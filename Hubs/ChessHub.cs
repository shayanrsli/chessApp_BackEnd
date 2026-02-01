using ChessServer.Models;
using ChessServer.Models.Enums;
using ChessServer.Services;
using Microsoft.AspNetCore.SignalR;

namespace ChessServer.Hubs
{
    public class ChessHub : Hub
    {
        private readonly ILogger<ChessHub> _logger;
        private readonly GameManager _gameManager;

        public ChessHub(ILogger<ChessHub> logger, GameManager gameManager)
        {
            _logger = logger;
            _gameManager = gameManager;
        }

        public string Ping()
        {
            _logger.LogInformation("Ping from {ConnectionId}", Context.ConnectionId);
            return $"Pong! {DateTime.UtcNow:O} | {Context.ConnectionId}";
        }

        public async Task<object> CreateGame(string gameName, string? playerName, string? playerId)
        {
            try
            {
                var room = _gameManager.CreateGame(gameName, isPrivate: true);
                var userId = string.IsNullOrWhiteSpace(playerId) ? Context.ConnectionId : playerId!;
                var username = string.IsNullOrWhiteSpace(playerName) ? $"Player_{Context.ConnectionId[..6]}" : playerName!;
                var now = DateTime.UtcNow;

                var player = new Player
                {
                    ConnectionId = Context.ConnectionId,
                    UserId = userId,
                    Username = username,
                    JoinedAt = now,
                    IsConnected = true,
                    LastSeenAt = now
                };

                _gameManager.WithRoomLock(room.RoomId, () =>
                {
                    room.WhitePlayer = player;
                    room.Status = GameStatus.WaitingForPlayer;
                    room.CurrentFen = ChessBoard.InitialFen;

                    // ✅ clock init
                    room.WhiteTimeLeft = room.InitialSeconds;
                    room.BlackTimeLeft = room.InitialSeconds;
                    room.ActiveColor = "white";
                    room.LastTickAtUtc = now;
                });

                _gameManager.IndexConnection(Context.ConnectionId, room.RoomId, userId);
                await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);

                var resp = new
                {
                    success = true,
                    roomId = room.RoomId,
                    inviteCode = room.InviteCode,
                    room = new
                    {
                        room.RoomId,
                        room.Name,
                        Status = room.Status.ToString(),
                        room.IsPrivate,
                        WhitePlayer = room.WhitePlayer?.Username ?? username
                    }
                };

                await Clients.Caller.SendAsync("GameCreated", resp);
                return resp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateGame failed");
                return new { success = false, message = "خطای سرور" };
            }
        }

        public async Task<object> EnsureJoined(string roomId, string? playerName = null, string? playerId = null)
        {
            try
            {
                var room = _gameManager.GetGame(roomId);
                if (room == null) return new { success = false, message = "بازی یافت نشد" };

                var userId = string.IsNullOrWhiteSpace(playerId) ? Context.ConnectionId : playerId!;
                var username = string.IsNullOrWhiteSpace(playerName) ? $"Player_{Context.ConnectionId[..6]}" : playerName!;
                var now = DateTime.UtcNow;

                // ✅ apply elapsed before snapshot
                _gameManager.WithRoomLock(room.RoomId, () => _gameManager.ApplyElapsedTime(room, now));

                // reconnect white
                if (room.WhitePlayer?.UserId == userId)
                {
                    _gameManager.WithRoomLock(room.RoomId, () =>
                    {
                        room.WhitePlayer!.ConnectionId = Context.ConnectionId;
                        room.WhitePlayer.Username = username;
                        room.WhitePlayer.IsConnected = true;
                        room.WhitePlayer.LastSeenAt = now;
                    });

                    _gameManager.IndexConnection(Context.ConnectionId, room.RoomId, userId);
                    await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);

                    return BuildSyncPayload(room, "white", isReconnecting: true);
                }

                // reconnect black
                if (room.BlackPlayer?.UserId == userId)
                {
                    _gameManager.WithRoomLock(room.RoomId, () =>
                    {
                        room.BlackPlayer!.ConnectionId = Context.ConnectionId;
                        room.BlackPlayer.Username = username;
                        room.BlackPlayer.IsConnected = true;
                        room.BlackPlayer.LastSeenAt = now;
                    });

                    _gameManager.IndexConnection(Context.ConnectionId, room.RoomId, userId);
                    await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);

                    return BuildSyncPayload(room, "black", isReconnecting: true);
                }

                // new join
                if (room.IsFull) return new { success = false, message = "بازی پر شده است" };
                if (room.BlackPlayer != null) return new { success = false, message = "بازیکن دوم قبلاً وارد شده" };
                if (room.WhitePlayer == null) return new { success = false, message = "بازی ناقص است" };

                var black = new Player
                {
                    ConnectionId = Context.ConnectionId,
                    UserId = userId,
                    Username = username,
                    JoinedAt = now,
                    IsConnected = true,
                    LastSeenAt = now
                };

                _gameManager.WithRoomLock(room.RoomId, () =>
                {
                    room.BlackPlayer = black;
                    room.Status = GameStatus.InProgress;
                    room.StartedAt = now;

                    // clock reset at start
                    room.WhiteTimeLeft = room.InitialSeconds;
                    room.BlackTimeLeft = room.InitialSeconds;
                    room.ActiveColor = "white";
                    room.LastTickAtUtc = now;
                });

                _gameManager.IndexConnection(Context.ConnectionId, room.RoomId, userId);
                await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);

                await Clients.Group(room.RoomId).SendAsync("GameStarted", new
                {
                    RoomId = room.RoomId,
                    Name = room.Name,
                    WhitePlayer = new { room.WhitePlayer.Username, room.WhitePlayer.UserId, room.WhitePlayer.ConnectionId },
                    BlackPlayer = new { room.BlackPlayer!.Username, room.BlackPlayer.UserId, room.BlackPlayer.ConnectionId },
                    Board = room.CurrentFen,
                    CurrentTurn = room.ActiveColor,
                    Status = "InProgress",

                    // ✅ clock snapshot
                    activeColor = room.ActiveColor,
                    whiteTimeLeft = room.WhiteTimeLeft,
                    blackTimeLeft = room.BlackTimeLeft,
                    serverNowUtc = now.ToString("O")
                });

                await Clients.Group(room.RoomId).SendAsync("PlayerJoined", new
                {
                    Player = new { Username = black.Username, UserId = black.UserId, Color = "black" },
                    RoomId = room.RoomId
                });

                return BuildSyncPayload(room, "black", isReconnecting: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EnsureJoined failed");
                return new { success = false, message = "خطای سرور" };
            }
        }

        public Task<object> JoinByInviteCode(string inviteCode, string? playerName = null, string? playerId = null)
        {
            var room = _gameManager.GetGameByInviteCode(inviteCode);
            if (room == null) return Task.FromResult<object>(new { success = false, message = "بازی یافت نشد" });
            return EnsureJoined(room.RoomId, playerName, playerId);
        }

        public async Task<object> MakeMove(string roomId, string from, string to, string? promotion = null, string? fenAfter = null)
        {
            try
            {
                var room = _gameManager.GetGame(roomId);
                if (room == null) return new { success = false, message = "بازی یافت نشد" };
                if (room.Status != GameStatus.InProgress) return new { success = false, message = "بازی شروع نشده است" };

                var now = DateTime.UtcNow;

                var userInfo = _gameManager.FindByConnection(Context.ConnectionId);
                var userId = userInfo?.UserId ?? Context.ConnectionId;

                object? broadcastPayload = null;

                var result = _gameManager.WithRoomLock(room.RoomId, () =>
                {
                    _gameManager.ApplyElapsedTime(room, now);

                    if (room.WhiteTimeLeft <= 0 || room.BlackTimeLeft <= 0)
                        return (object)new { success = false, message = "⏰ زمان بازی تمام شده است" };

                    var expectedUserId = room.ActiveColor == "white" ? room.WhitePlayer?.UserId : room.BlackPlayer?.UserId;
                    if (expectedUserId == null) return (object)new { success = false, message = "بازیکن ناقص است" };

                    if (expectedUserId != userId)
                        return (object)new { success = false, message = "نوبت شما نیست" };

                    var moverColor = room.ActiveColor;

                    var move = new Move
                    {
                        From = from,
                        To = to,
                        Promotion = promotion,
                        PlayerUserId = userId,
                        PlayerConnectionId = Context.ConnectionId,
                        Timestamp = now,
                        FenAfter = fenAfter
                    };

                    room.Moves.Add(move);

                    if (!string.IsNullOrWhiteSpace(fenAfter))
                        room.CurrentFen = fenAfter!;

                    _gameManager.SwitchTurnAndIncrement(room, moverColor);

                    broadcastPayload = new
                    {
                        success = true,
                        roomId,
                        from,
                        to,
                        promotion,
                        byUserId = userId,
                        color = moverColor,
                        nextTurn = room.ActiveColor,
                        moveNumber = room.Moves.Count,
                        fen = room.CurrentFen,

                        // ✅ clock snapshot
                        activeColor = room.ActiveColor,
                        whiteTimeLeft = room.WhiteTimeLeft,
                        blackTimeLeft = room.BlackTimeLeft,
                        serverNowUtc = now.ToString("O")
                    };

                    return (object)new { success = true };
                });

                if (broadcastPayload != null)
                    await Clients.Group(roomId).SendAsync("MoveMade", broadcastPayload);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MakeMove failed");
                return new { success = false, message = "خطای سرور" };
            }
        }

        public async Task<object> SendGameMessage(string roomId, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return new { success = false, message = "پیام خالی است" };

            var room = _gameManager.GetGame(roomId);
            if (room == null) return new { success = false, message = "بازی یافت نشد" };

            var userInfo = _gameManager.FindByConnection(Context.ConnectionId);
            var userId = userInfo?.UserId ?? Context.ConnectionId;

            var sender =
                room.WhitePlayer?.UserId == userId ? room.WhitePlayer!.Username :
                room.BlackPlayer?.UserId == userId ? room.BlackPlayer!.Username :
                "Unknown";

            await Clients.Group(roomId).SendAsync("GameMessage", new
            {
                roomId,
                sender,
                text = message.Trim(),
                timestamp = DateTime.UtcNow
            });

            return new { success = true };
        }

        public Task<object> GetGameStatus(string roomId)
        {
            var room = _gameManager.GetGame(roomId);
            if (room == null) return Task.FromResult<object>(new { success = false, message = "بازی یافت نشد" });

            var now = DateTime.UtcNow;
            _gameManager.WithRoomLock(room.RoomId, () => _gameManager.ApplyElapsedTime(room, now));

            return Task.FromResult<object>(new
            {
                success = true,
                roomId = room.RoomId,
                status = room.Status.ToString(),
                white = room.WhitePlayer is null ? null : new { room.WhitePlayer.Username, room.WhitePlayer.UserId },
                black = room.BlackPlayer is null ? null : new { room.BlackPlayer.Username, room.BlackPlayer.UserId },
                fen = room.CurrentFen,
                moveCount = room.Moves.Count,
                currentTurn = room.ActiveColor,

                activeColor = room.ActiveColor,
                whiteTimeLeft = room.WhiteTimeLeft,
                blackTimeLeft = room.BlackTimeLeft,
                serverNowUtc = now.ToString("O")
            });
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Connected: {ConnectionId}", Context.ConnectionId);
            await Clients.Caller.SendAsync("Connected", new
            {
                message = "Connected",
                connectionId = Context.ConnectionId,
                serverTime = DateTime.UtcNow
            });
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Disconnected: {ConnectionId} | {Error}", Context.ConnectionId, exception?.Message);

            _gameManager.MarkPlayerDisconnected(Context.ConnectionId);

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                _gameManager.RemovePlayerIfStillDisconnected(Context.ConnectionId, TimeSpan.FromSeconds(30));
            });

            await base.OnDisconnectedAsync(exception);
        }

        private object BuildSyncPayload(GameRoom room, string yourColor, bool isReconnecting)
        {
            var opponentName = yourColor == "white" ? room.BlackPlayer?.Username : room.WhitePlayer?.Username;
            var now = DateTime.UtcNow;

            return new
            {
                success = true,
                roomId = room.RoomId,
                yourColor,
                isReconnecting,
                status = room.Status.ToString(),
                opponent = opponentName ?? "در انتظار حریف",
                fen = room.CurrentFen,
                currentTurn = room.ActiveColor,
                moveCount = room.Moves.Count,

                // ✅ clock snapshot
                activeColor = room.ActiveColor,
                whiteTimeLeft = room.WhiteTimeLeft,
                blackTimeLeft = room.BlackTimeLeft,
                serverNowUtc = now.ToString("O")
            };
        }
    }
}

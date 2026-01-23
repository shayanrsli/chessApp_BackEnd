                using Microsoft.AspNetCore.SignalR;
                using ChessServer.Models;
                using ChessServer.Services;
                using ChessServer.Models.Enums;
                using System.Threading.Tasks;
                using System.Linq;
                using System.Text.Json;

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
        
        // اضافه کردن لاگ برای اطمینان
        Console.WriteLine($"🎯 ChessHub initialized, GameManager is null: {_gameManager == null}");
    }

                        
                        // ========== متدهای تست ==========
                        public string Ping()
                        {
                            _logger.LogInformation($"Ping from {Context.ConnectionId}");
                            return $"Pong! Server time: {DateTime.Now:HH:mm:ss}, Your ID: {Context.ConnectionId}";
                        }
                        
                        public async Task<object> TestConnection(string message)
                        {
                            _logger.LogInformation($"Test from {Context.ConnectionId}: {message}");
                            
                            await Clients.Caller.SendAsync("TestResponse", 
                                $"Echo: {message} at {DateTime.Now:HH:mm:ss}");
                                
                            return new
                            {
                                Success = true,
                                Message = $"Received: {message}",
                                Timestamp = DateTime.UtcNow,
                                ConnectionId = Context.ConnectionId
                            };
                        }
                        
                        // ========== متدهای بازی ==========
                        
                        public object GetPublicGames()
                        {
                            try
                            {
                                var games = _gameManager.GetAllGames()
                                    .Where(g => !g.IsPrivate && !g.IsFull)
                                    .Select(g => new
                                    {
                                        g.RoomId,
                                        g.Name,
                                        WhitePlayer = g.WhitePlayer?.Username ?? "Waiting",
                                        BlackPlayer = g.BlackPlayer?.Username ?? "Waiting",
                                        Status = g.Status.ToString(),
                                        PlayerCount = (g.WhitePlayer != null ? 1 : 0) + (g.BlackPlayer != null ? 1 : 0),
                                        MaxPlayers = 2
                                    })
                                    .ToList();
                                
                                return new
                                {
                                    Success = true,
                                    Games = games,
                                    Count = games.Count
                                };
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error getting public games");
                                return new { Success = false, Error = ex.Message };
                            }
                        }



public async Task<object> CreateGame(
    string gameName,
    string? playerName,
    string? playerId)
{
    try
    {
        Console.WriteLine($"\n🎯 ===== CREATE GAME START =====");
        Console.WriteLine($"📋 GameName: {gameName}");
        Console.WriteLine($"👤 PlayerName: {playerName}");
        Console.WriteLine($"🆔 PlayerId: {playerId}");
        Console.WriteLine($"🔗 ConnectionId: {Context.ConnectionId}");

        var safeId = Context.ConnectionId.Length > 6
            ? Context.ConnectionId.Substring(0, 6)
            : Context.ConnectionId;

        var player = new Player
        {
            ConnectionId = Context.ConnectionId,
            UserId = playerId ?? Context.ConnectionId,
            Username = playerName ?? $"Player_{safeId}",
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        };

        // 🎮 ایجاد بازی (private پیش‌فرض)
        var room = _gameManager.CreateGame(gameName, true);
        if (room == null)
        {
            return new { success = false, message = "خطا در ایجاد بازی" };
        }

        room.WhitePlayer = player;

        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);

        var response = new
        {
            success = true,
            roomId = room.RoomId,
            inviteCode = room.InviteCode,
            inviteLink = $"http://localhost:5173/join?code={room.InviteCode}",
            room = new
            {
                room.RoomId,
                room.Name,
                Status = room.Status.ToString(),
                room.IsPrivate,
                WhitePlayer = room.WhitePlayer.Username
            }
        };

        await Clients.Caller.SendAsync("GameCreated", response);

        Console.WriteLine($"✅ Game created successfully: {room.RoomId}");
        Console.WriteLine($"🎯 ===== CREATE GAME END =====\n");

        return response;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"💥 CRASH in CreateGame: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
        return new { success = false, message = "خطای سرور", error = ex.Message };
    }
}

public async Task<object> JoinGame(string roomId, string? playerName = null, string? playerId = null)
{
    try
    {
        Console.WriteLine($"\n🎮 JOIN GAME START: Room={roomId}, PlayerId={playerId}");
        
        var room = _gameManager.GetGame(roomId);
        if (room == null)
            return new { success = false, message = "بازی یافت نشد" };
        
        // 🔥 1. چک برای reconnect (با UserId)
        if (!string.IsNullOrEmpty(playerId))
        {
            if (room.WhitePlayer?.UserId == playerId)
            {
                // reconnect سفید
                room.WhitePlayer.ConnectionId = Context.ConnectionId;
                room.WhitePlayer.IsConnected = true;
                await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
                return new { success = true, yourColor = "white", isReconnecting = true };
            }
            if (room.BlackPlayer?.UserId == playerId)
            {
                // reconnect سیاه
                room.BlackPlayer.ConnectionId = Context.ConnectionId;
                room.BlackPlayer.IsConnected = true;
                await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
                return new { success = true, yourColor = "black", isReconnecting = true };
            }
        }
        
        // 🔥 2. پیشگیری از double join با ConnectionId
        if (room.WhitePlayer?.ConnectionId == Context.ConnectionId || 
            room.BlackPlayer?.ConnectionId == Context.ConnectionId)
            return new { success = false, message = "قبلاً وارد شده‌اید" };
        
        // 🔥 3. اضافه کردن بازیکن جدید (فقط به عنوان سیاه!)
        if (room.IsFull)
            return new { success = false, message = "بازی پر شده است" };
        
        // 🔥 فقط سیاه می‌تواند join کند
        if (room.BlackPlayer != null)
            return new { success = false, message = "بازیکن دوم قبلاً وارد شده" };
        
        var player = new Player
        {
            ConnectionId = Context.ConnectionId,
            UserId = playerId ?? Context.ConnectionId,
            Username = playerName ?? $"Player_{Context.ConnectionId.Substring(0, 6)}",
            JoinedAt = DateTime.UtcNow,
            IsConnected = true
        };
        
        // 🔥 همیشه بازیکن دوم = سیاه
        room.BlackPlayer = player;
        string yourColor = "black";
        
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        
        // 🔥 4. اگر بازی کامل شد، GameStarted بفرست
        if (room.IsFull && room.Status == GameStatus.WaitingForPlayer)
        {
            room.Status = GameStatus.InProgress;
            room.StartedAt = DateTime.UtcNow;
            
            await Clients.Group(roomId).SendAsync("GameStarted", new
            {
                RoomId = room.RoomId,
                WhitePlayer = new { 
                    room.WhitePlayer.Username, 
                    room.WhitePlayer.UserId,
                    room.WhitePlayer.ConnectionId 
                },
                BlackPlayer = new { 
                    room.BlackPlayer.Username, 
                    room.BlackPlayer.UserId,
                    room.BlackPlayer.ConnectionId 
                },
                CurrentTurn = "white",
                Board = ChessBoard.InitialFen
            });
            
            Console.WriteLine($"🚀 GAME STARTED: {roomId}");
        }
        else
        {
            // اطلاع به سفید که سیاه join کرده
            await Clients.Group(roomId).SendAsync("PlayerJoined", new
            {
                Player = new
                {
                    Username = player.Username,
                    UserId = player.UserId,
                    Color = "black"
                },
                RoomId = roomId
            });
        }
        
        return new { 
            success = true, 
            yourColor = yourColor,
            opponent = room.WhitePlayer?.Username ?? "در انتظار حریف",
            roomId = room.RoomId
        };
    }
    catch (Exception ex)
    {
        Console.WriteLine($"💥 JoinGame error: {ex}");
        return new { success = false, message = "خطای سرور" };
    }
}


public async Task<object> JoinByInviteCode(
    string inviteCode,
    string? playerName = null,
    string? playerId = null)
{
    Console.WriteLine($"🎯 [JoinByInviteCode] Code={inviteCode} PlayerId={playerId}");

    if (string.IsNullOrWhiteSpace(inviteCode))
        return new { success = false, message = "کد دعوت نامعتبر است" };

    var room = _gameManager.GetGameByInviteCode(inviteCode.Trim());
    if (room == null)
        return new { success = false, message = "بازی یافت نشد" };

    // 🔁 RECONNECT با UserId
    if (!string.IsNullOrEmpty(playerId))
    {
        if (room.WhitePlayer?.UserId == playerId)
        {
            room.WhitePlayer.ConnectionId = Context.ConnectionId;
            room.WhitePlayer.IsConnected = true;
            await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);
            return new { success = true, roomId = room.RoomId, yourColor = "white", isReconnecting = true };
        }

        if (room.BlackPlayer?.UserId == playerId)
        {
            room.BlackPlayer.ConnectionId = Context.ConnectionId;
            room.BlackPlayer.IsConnected = true;
            await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);
            return new { success = true, roomId = room.RoomId, yourColor = "black", isReconnecting = true };
        }
    }

    // 🔥 پیشگیری از double join با ConnectionId
    if (room.WhitePlayer?.ConnectionId == Context.ConnectionId || 
        room.BlackPlayer?.ConnectionId == Context.ConnectionId)
        return new { success = false, message = "قبلاً وارد شده‌اید" };
    
    // ❌ اگر بازیکن سیاه قبلاً هست → خطا
    if (room.BlackPlayer != null)
        return new { success = false, message = "بازیکن دوم قبلاً وارد شده" };

    // ✅ ساخت بازیکن دوم
    var safeId = Context.ConnectionId[..6];
    var blackPlayer = new Player
    {
        ConnectionId = Context.ConnectionId,
        UserId = playerId ?? Context.ConnectionId,
        Username = playerName ?? $"Player_{safeId}",
        JoinedAt = DateTime.UtcNow,
        IsConnected = true
    };

    room.BlackPlayer = blackPlayer;
    room.Status = GameStatus.InProgress;
    room.StartedAt = DateTime.UtcNow;
    room.Board ??= new ChessBoard();

    await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);

    // 🚀 ارسال GameStarted
    await Clients.Group(room.RoomId).SendAsync("GameStarted", new
    {
        RoomId = room.RoomId,
        Name = room.Name,
        WhitePlayer = new { room.WhitePlayer.Username, room.WhitePlayer.UserId, room.WhitePlayer.ConnectionId },
        BlackPlayer = new { blackPlayer.Username, blackPlayer.UserId, blackPlayer.ConnectionId },
        Board = ChessBoard.InitialFen,
        CurrentTurn = "white",
        Status = "InProgress"
    });

    Console.WriteLine($"🚀 GAME STARTED via invite: {room.RoomId}");

    return new
    {
        success = true,
        roomId = room.RoomId,
        yourColor = "black",
        opponent = room.WhitePlayer.Username
    };
}



  
                    public async Task<object> MakeMove(string roomId, string from, string to, string? promotion = null)
                        {
                            try
                            {
                                var room = _gameManager.GetGame(roomId);
                                if (room == null)
                                    return new { Success = false, Message = "بازی یافت نشد" };
                                    
                                if (room.Status != GameStatus.InProgress)
                                    return new { Success = false, Message = "بازی شروع نشده است" };
                                
                                // تعیین نوبت فعلی
                                var isWhiteTurn = room.Moves.Count % 2 == 0;
                                var currentPlayer = isWhiteTurn ? room.WhitePlayer : room.BlackPlayer;
                                
                                // بررسی اینکه آیا نوبت بازیکن فعلی است
                                if (currentPlayer?.ConnectionId != Context.ConnectionId)
                                    return new { Success = false, Message = "نوبت شما نیست" };
                                    
                                var move = new Move
                                {
                                    From = from,
                                    To = to,
                                    Promotion = promotion,
                                    PlayerConnectionId = Context.ConnectionId,
                                    Timestamp = DateTime.UtcNow
                                };
                                
                                // افزودن حرکت
                                room.Moves.Add(move);
                                
                                // ارسال حرکت به همه بازیکنان
                                await Clients.Group(roomId).SendAsync("MoveMade", new
                                {
                                    Success = true,
                                    From = from,
                                    To = to,
                                    Promotion = promotion,
                                    Player = currentPlayer.Username,
                                    Color = isWhiteTurn ? "white" : "black",
                                    NextTurn = !isWhiteTurn ? "white" : "black",
                                    MoveNumber = room.Moves.Count,
                                    IsCheck = false,
                                    IsCheckmate = false
                                });
                                
                                // لاگ حرکت
                                Console.WriteLine($"♟️ [{DateTime.Now:HH:mm:ss}] Move: {from}-{to} by {currentPlayer.Username} in {roomId}");
                                
                                return new { Success = true, Message = "حرکت ثبت شد" };
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error making move");
                                return new { Success = false, Error = ex.Message };
                            }
                        }
                        
                        public async Task<object> ResignGame(string roomId)
                        {
                            try
                            {
                                var room = _gameManager.GetGame(roomId);
                                if (room == null)
                                    return new { Success = false, Message = "بازی یافت نشد" };
                                
                                var player = room.WhitePlayer?.ConnectionId == Context.ConnectionId ? 
                                            room.WhitePlayer : room.BlackPlayer;
                                            
                                if (player == null)
                                    return new { Success = false, Message = "بازیکن یافت نشد" };
                                    
                                room.Status = GameStatus.Finished;
                                
                                await Clients.Group(roomId).SendAsync("PlayerResigned", new
                                {
                                    Player = player.Username,
                                    Color = room.WhitePlayer?.ConnectionId == Context.ConnectionId ? "white" : "black",
                                    RoomId = roomId,
                                    Winner = room.WhitePlayer?.ConnectionId == Context.ConnectionId ? 
                                            room.BlackPlayer?.Username : room.WhitePlayer?.Username
                                });
                                
                                return new { Success = true, Message = "استعفا ثبت شد" };
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error resigning game");
                                return new { Success = false, Error = ex.Message };
                            }
                        }
                        
                        public async Task<object> OfferDraw(string roomId)
                        {
                            try
                            {
                                var room = _gameManager.GetGame(roomId);
                                if (room == null)
                                    return new { Success = false, Message = "بازی یافت نشد" };
                                
                                var player = room.WhitePlayer?.ConnectionId == Context.ConnectionId ? 
                                            room.WhitePlayer : room.BlackPlayer;
                                            
                                if (player == null)
                                    return new { Success = false, Message = "بازیکن یافت نشد" };
                                    
                                await Clients.OthersInGroup(roomId).SendAsync("DrawOffered", new
                                {
                                    By = player.Username,
                                    Color = room.WhitePlayer?.ConnectionId == Context.ConnectionId ? "white" : "black",
                                    RoomId = roomId,
                                    Timestamp = DateTime.UtcNow
                                });
                                
                                return new { Success = true, Message = "پیشنهاد تساوی ارسال شد" };
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error offering draw");
                                return new { Success = false, Error = ex.Message };
                            }
                        }
                        
                        public async Task<object> SendGameMessage(string roomId, string message)
                        {
                            try
                            {
                                if (string.IsNullOrWhiteSpace(message))
                                    return new { Success = false, Message = "پیام نمی‌تواند خالی باشد" };
                                    
                                var room = _gameManager.GetGame(roomId);
                                if (room == null)
                                    return new { Success = false, Message = "بازی یافت نشد" };
                                
                                var player = room.WhitePlayer?.ConnectionId == Context.ConnectionId ? 
                                            room.WhitePlayer : room.BlackPlayer;
                            
                                if (player == null)
                                    return new { Success = false, Message = "بازیکن یافت نشد" };
                            
                                await Clients.Group(roomId).SendAsync("GameMessage", new
                                {
                                    Sender = player.Username ?? "Unknown",
                                    Message = message,
                                    Timestamp = DateTime.UtcNow,
                                    Color = room.WhitePlayer?.ConnectionId == Context.ConnectionId ? "white" : "black"
                                });
                                
                                return new { Success = true, Message = "پیام ارسال شد" };
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending game message");
                                return new { Success = false, Error = ex.Message };
                            }
                        }
                        
                        // متد جدید برای بازیابی وضعیت بازی
                        public async Task<object> GetGameStatus(string roomId)
                        {
                            try
                            {
                                var room = _gameManager.GetGame(roomId);
                                if (room == null)
                                    return new { Success = false, Message = "بازی یافت نشد" };
                                
                                var currentPlayerColor = room.WhitePlayer?.ConnectionId == Context.ConnectionId ? "white" : "black";
                                
                                return new
                                {
                                    Success = true,
                                    Room = new
                                    {
                                        room.RoomId,
                                        room.Name,
                                        Status = room.Status.ToString(),
                                        room.IsPrivate,
                                        WhitePlayer = room.WhitePlayer != null ? new
                                        {
                                            room.WhitePlayer.Username,
                                            room.WhitePlayer.ConnectionId
                                        } : null,
                                        BlackPlayer = room.BlackPlayer != null ? new
                                        {
                                            room.BlackPlayer.Username,
                                            room.BlackPlayer.ConnectionId
                                        } : null,
                                        room.StartedAt,
                                        room.CreatedAt,
                                        MoveCount = room.Moves.Count
                                    },
                                    YourColor = currentPlayerColor,
                                    CurrentTurn = room.Moves.Count % 2 == 0 ? "white" : "black"
                                };
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error getting game status");
                                return new { Success = false, Error = ex.Message };
                            }
                        }
                        
                        // متد جدید برای تست WebSocket
                        public async Task<string> TestWebSocket(string message)
                        {
                            _logger.LogInformation($"WebSocket test from {Context.ConnectionId}: {message}");
                            
                            // تست ارسال پیام در زمان‌های مختلف
                            await Task.Delay(100);
                            await Clients.Caller.SendAsync("TestMessage", $"Echo: {message}");
                            
                            await Task.Delay(100);
                            await Clients.Caller.SendAsync("TestMessage", $"Second message");
                            
                            return $"WebSocket test successful! Sent 2 messages. Your message: {message}";
                        }
                        
                        // ========== مدیریت اتصال ==========
                        
                        public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation($"🔗 Connected: {connectionId}");

        _gameManager.MarkPlayerReconnected(connectionId);

        await Clients.Caller.SendAsync("Connected", new
        {
            Message = "به سرور شطرنج خوش آمدید!",
            ConnectionId = connectionId,
            ServerTime = DateTime.UtcNow
        });

        await base.OnConnectedAsync();
    }


    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation($"🔌 Disconnected: {connectionId}, Exception: {exception?.Message}");

        _gameManager.MarkPlayerDisconnected(connectionId);

        // حذف بازیکن بعد از 30 ثانیه اگر وصل نشد
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            _gameManager.RemovePlayerIfStillDisconnected(connectionId, TimeSpan.FromSeconds(30));
        });

        await base.OnDisconnectedAsync(exception);
    }




                    }
                }
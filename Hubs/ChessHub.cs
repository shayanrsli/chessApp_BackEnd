using Microsoft.AspNetCore.SignalR;
using ChessServer.Models;
using ChessServer.Services;
using ChessServer.Models.Enums;
using System.Threading.Tasks;
using System.Linq;
namespace ChessServer.Hubs
{
    public class ChessHub : Hub
    {
        private readonly GameManager _gameManager;
        private readonly ILogger<ChessHub> _logger;
        
        public ChessHub(GameManager gameManager, ILogger<ChessHub> logger)
        {
            _gameManager = gameManager;
            _logger = logger;
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
        
        public async Task<object> CreateGame(string gameName = "Chess Game", bool isPrivate = false, string? playerName = null)
        {
            try
            {
                _logger.LogInformation($"Creating game: {gameName}, Private: {isPrivate}");
                
                var player = new Player
                {
                    ConnectionId = Context.ConnectionId,
                    UserId = Context.UserIdentifier ?? Context.ConnectionId,
                    Username = playerName ?? $"Player_{Context.ConnectionId[..6]}",
                    JoinedAt = DateTime.UtcNow
                };
                
                var room = _gameManager.CreateGame(gameName, isPrivate);
                room.WhitePlayer = player;
                
                // اضافه کردن به گروه
                await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);
                
                _logger.LogInformation($"Game created: {room.RoomId} by {player.Username}");
                Console.WriteLine($"🎮 [{DateTime.Now:HH:mm:ss}] Game created: {room.RoomId}, InviteCode: {room.InviteCode}");
                
                var response = new
                {
                    Success = true,
                    RoomId = room.RoomId,
                    InviteCode = room.InviteCode,
                    InviteLink = isPrivate ? $"http://localhost:5173/join?code={room.InviteCode}" : null,
                    Room = new
                    {
                        room.RoomId,
                        room.Name,
                        Status = room.Status.ToString(),
                        room.IsPrivate,
                        WhitePlayer = room.WhitePlayer?.Username,
                        BlackPlayer = room.BlackPlayer?.Username,
                        CreatedAt = room.CreatedAt
                    }
                };
                
                await Clients.Caller.SendAsync("GameCreated", response);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating game");
                return new { Success = false, Error = ex.Message };
            }
        }
        
        public async Task<object> JoinGame(string roomId, string? playerName = null)
        {
            try
            {
                _logger.LogInformation($"Joining game: {roomId}");
                
                if (string.IsNullOrEmpty(roomId))
                {
                    return new { Success = false, Message = "شناسه بازی نامعتبر است" };
                }
                
                var player = new Player
                {
                    ConnectionId = Context.ConnectionId,
                    UserId = Context.UserIdentifier ?? Context.ConnectionId,
                    Username = playerName ?? $"Player_{Context.ConnectionId[..6]}",
                    JoinedAt = DateTime.UtcNow
                };
                
                var room = _gameManager.GetGame(roomId);
                
                if (room == null)
                {
                    return new { Success = false, Message = "بازی یافت نشد" };
                }
                
                if (room.IsFull)
                {
                    return new { Success = false, Message = "بازی پر شده است" };
                }
                
                // اگر بازیکن در حال پیوستن به بازی خودش باشد
                if (room.WhitePlayer?.ConnectionId == Context.ConnectionId)
                {
                    // بازیکن دوباره وصل شده
                    return new 
                    { 
                        Success = true, 
                        RoomId = roomId,
                        YourColor = "white",
                        Opponent = room.BlackPlayer?.Username,
                        IsReconnecting = true
                    };
                }
                
                // اضافه کردن بازیکن دوم
                room.BlackPlayer = player;
                
                // اضافه کردن به گروه
                await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
                
                // اگر بازی شروع شد
                if (room.IsFull)
                {
                    room.Status = GameStatus.InProgress;
                    room.StartedAt = DateTime.UtcNow;
                    
                    await Clients.Group(roomId).SendAsync("GameStarted", new
                    {
                        RoomId = roomId,
                        StartedAt = room.StartedAt,
                        WhitePlayer = new
                        {
                            room.WhitePlayer?.Username,
                            room.WhitePlayer?.ConnectionId
                        },
                        BlackPlayer = new
                        {
                            room.BlackPlayer?.Username,
                            room.BlackPlayer?.ConnectionId
                        },
                        Board = room.Board?.GetCurrentBoard(),
                        CurrentTurn = "white"
                    });
                    
                    _logger.LogInformation($"🚀 Game started: {roomId}");
                    Console.WriteLine($"🚀 [{DateTime.Now:HH:mm:ss}] Game started: {roomId}");
                }
                else
                {
                    // اطلاع به سایر بازیکنان
                    await Clients.Group(roomId).SendAsync("PlayerJoined", new
                    {
                        Player = new
                        {
                            player.Username,
                            player.ConnectionId
                        },
                        Room = new
                        {
                            room.RoomId,
                            room.Name,
                            Status = room.Status.ToString(),
                            Players = new
                            {
                                White = room.WhitePlayer?.Username,
                                Black = room.BlackPlayer?.Username
                            }
                        }
                    });
                }
                
                return new
                {
                    Success = true,
                    RoomId = roomId,
                    YourColor = "black",
                    Opponent = room.WhitePlayer?.Username,
                    Room = new
                    {
                        room.RoomId,
                        room.Name,
                        Status = room.Status.ToString(),
                        WhitePlayer = room.WhitePlayer?.Username,
                        BlackPlayer = room.BlackPlayer?.Username
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining game");
                return new { Success = false, Error = ex.Message };
            }
        }
        
        public async Task<object> JoinByInviteCode(string inviteCode, string? playerName = null)
        {
            try
            {
                _logger.LogInformation($"🔍 Searching for game with invite code: {inviteCode}");
                Console.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss}] Searching for invite code: {inviteCode}");
                
                // پیدا کردن اتاق با کد دعوت
                var room = _gameManager.GetGameByInviteCode(inviteCode);
                
                if (room == null)
                {
                    _logger.LogWarning($"❌ Game not found for invite code: {inviteCode}");
                    Console.WriteLine($"❌ [{DateTime.Now:HH:mm:ss}] Invite code not found: {inviteCode}");
                    return new { 
                        Success = false, 
                        Message = "کد دعوت نامعتبر است",
                        RoomId = (string?)null 
                    };
                }
                
                _logger.LogInformation($"✅ Game found: {room.RoomId}");
                Console.WriteLine($"✅ [{DateTime.Now:HH:mm:ss}] Game found: {room.RoomId}, InviteCode: {room.InviteCode}");
                
                // اگر بازیکن در حال پیوستن به بازی خودش باشد
                if (room.WhitePlayer?.ConnectionId == Context.ConnectionId)
                {
                    // برگرد به گروه
                    await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);
                    
                    return new
                    {
                        Success = true,
                        RoomId = room.RoomId,
                        YourColor = "white",
                        Opponent = room.BlackPlayer?.Username,
                        IsReconnecting = true
                    };
                }
                
                if (room.IsFull)
                {
                    return new { 
                        Success = false, 
                        Message = "بازی پر شده است",
                        RoomId = room.RoomId 
                    };
                }
                
                var player = new Player
                {
                    ConnectionId = Context.ConnectionId,
                    UserId = Context.UserIdentifier ?? Context.ConnectionId,
                    Username = playerName ?? $"Player_{Context.ConnectionId[..6]}",
                    JoinedAt = DateTime.UtcNow
                };
                
                // اضافه کردن بازیکن دوم (سیاه)
                room.BlackPlayer = player;
                
                // اضافه کردن به گروه SignalR
                await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);
                
                // اگر بازی کامل شد، شروع کن
                if (room.IsFull)
                {
                    room.Status = GameStatus.InProgress;
                    room.StartedAt = DateTime.UtcNow;
                    
                    // اطلاع به همه بازیکنان
                    await Clients.Group(room.RoomId).SendAsync("GameStarted", new
                    {
                        RoomId = room.RoomId,
                        Name = room.Name,
                        WhitePlayer = new
                        {
                            room.WhitePlayer?.Username,
                            room.WhitePlayer?.ConnectionId
                        },
                        BlackPlayer = new
                        {
                            room.BlackPlayer?.Username,
                            room.BlackPlayer?.ConnectionId
                        },
                        StartedAt = room.StartedAt,
                        Status = room.Status.ToString(),
                        Board = room.Board?.GetCurrentBoard(),
                        CurrentTurn = "white"
                    });
                    
                    _logger.LogInformation($"🚀 Game started: {room.RoomId}");
                    Console.WriteLine($"🚀 [{DateTime.Now:HH:mm:ss}] Game started: {room.RoomId}");
                }
                else
                {
                    // اطلاع به بازیکن اول
                    await Clients.Group(room.RoomId).SendAsync("PlayerJoined", new
                    {
                        Player = new
                        {
                            player.Username,
                            player.ConnectionId
                        },
                        Room = new
                        {
                            room.RoomId,
                            room.Name,
                            Status = room.Status.ToString(),
                            Players = new
                            {
                                White = room.WhitePlayer?.Username,
                                Black = room.BlackPlayer?.Username
                            }
                        }
                    });
                }
                
                return new
                {
                    Success = true,
                    RoomId = room.RoomId,
                    YourColor = "black",
                    Opponent = room.WhitePlayer?.Username
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error joining by invite code");
                Console.WriteLine($"❌ [{DateTime.Now:HH:mm:ss}] Error: {ex.Message}");
                return new { 
                    Success = false, 
                    Error = ex.Message,
                    RoomId = (string?)null 
                };
            }
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
            var userAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"].ToString();
            var origin = Context.GetHttpContext()?.Request.Headers["Origin"].ToString();
            
            _logger.LogInformation($"🎯 NEW CONNECTION: {connectionId}");
            _logger.LogInformation($"🌐 Origin: {origin}");
            _logger.LogInformation($"🖥️ User-Agent: {userAgent}");
            
            // پاسخ به کاربر
            await Clients.Caller.SendAsync("Connected", new
            {
                Message = "به سرور شطرنج خوش آمدید!",
                ConnectionId = connectionId,
                ServerTime = DateTime.UtcNow,
                Version = "1.0.0",
                Features = new
                {
                    GameCreation = true,
                    RealTimeChat = true,
                    Multiplayer = true
                }
            });
            
            // لاگ به کنسول
            Console.WriteLine($"✅ [{DateTime.Now:HH:mm:ss}] Client connected: {connectionId}");
            
            await base.OnConnectedAsync();
        }
        
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            
            if (exception != null)
            {
                _logger.LogWarning($"Client disconnected with error: {connectionId}, {exception.Message}");
                Console.WriteLine($"❌ [{DateTime.Now:HH:mm:ss}] Client disconnected with error: {connectionId}");
            }
            else
            {
                _logger.LogInformation($"Client disconnected: {connectionId}");
                Console.WriteLine($"🔌 [{DateTime.Now:HH:mm:ss}] Client disconnected: {connectionId}");
            }
            
            // حذف بازیکن از بازی‌ها
            _gameManager.RemovePlayer(connectionId);
            
            // اطلاع به سایر بازیکنان
            await Clients.All.SendAsync("PlayerDisconnected", new
            {
                ConnectionId = connectionId,
                Timestamp = DateTime.UtcNow
            });
            
            await base.OnDisconnectedAsync(exception);
        }
    }
}
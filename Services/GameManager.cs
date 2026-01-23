    using ChessServer.Models;
    using ChessServer.Models.Enums;
    using System.Collections.Concurrent;

    namespace ChessServer.Services
    {
        public class GameManager
        {
            private readonly ConcurrentDictionary<string, GameRoom> _games = new();
            private readonly ConcurrentDictionary<string, string> _playerToGame = new();
            

// در GameManager.cs
public GameRoom CreateGame(string name, bool isPrivate)
{
    try
    {
        Console.WriteLine($"🎮 GameManager.CreateGame called: {name}, Private: {isPrivate}");
        
        var room = new GameRoom
        {
            RoomId = Guid.NewGuid().ToString(),
            Name = name,
            IsPrivate = isPrivate,
            Status = GameStatus.Waiting,
            CreatedAt = DateTime.UtcNow,
            Board = new ChessBoard()
        };

        if (isPrivate)
        {
            room.InviteCode = GenerateInviteCode();
            Console.WriteLine($"🔑 Generated invite code: {room.InviteCode}");
        }

        _games[room.RoomId] = room;
        Console.WriteLine($"✅ Room created: {room.RoomId}, Total games: {_games.Count}");
        
        return room;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"💥 ERROR in GameManager.CreateGame: {ex.Message}");
        throw;
    }
}

private string GenerateInviteCode()
{
    return Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
}


            public void MarkPlayerReconnected(string connectionId)
            {
                var game = _games.Values.FirstOrDefault(g =>
                    g.WhitePlayer?.ConnectionId == connectionId ||
                    g.BlackPlayer?.ConnectionId == connectionId);

                if (game == null) return;

                var player = game.WhitePlayer?.ConnectionId == connectionId 
                    ? game.WhitePlayer 
                    : game.BlackPlayer;

                if (player == null) return;

                player.IsConnected = true;
                player.DisconnectedAt = null;
                
                Console.WriteLine($"♻️ Player reconnected: {connectionId}");
            }

            public void MarkPlayerDisconnected(string connectionId)
            {
                var game = _games.Values.FirstOrDefault(g =>
                    g.WhitePlayer?.ConnectionId == connectionId ||
                    g.BlackPlayer?.ConnectionId == connectionId);

                if (game == null) return;

                var player = game.WhitePlayer?.ConnectionId == connectionId 
                    ? game.WhitePlayer 
                    : game.BlackPlayer;

                if (player == null) return;

                player.IsConnected = false;
                player.DisconnectedAt = DateTime.UtcNow;
                
                Console.WriteLine($"🔌 Player disconnected: {connectionId}");
            }

            public void RemovePlayerIfStillDisconnected(string connectionId, TimeSpan timeout)
            {
                var game = _games.Values.FirstOrDefault(g =>
                    g.WhitePlayer?.ConnectionId == connectionId ||
                    g.BlackPlayer?.ConnectionId == connectionId);

                if (game == null) return;

                var player = game.WhitePlayer?.ConnectionId == connectionId 
                    ? game.WhitePlayer 
                    : game.BlackPlayer;

                if (player == null) return;

                if (player.IsConnected) return;

                if (player.DisconnectedAt.HasValue &&
                    DateTime.UtcNow - player.DisconnectedAt > timeout)
                {
                    if (game.WhitePlayer == player)
                    {
                        game.WhitePlayer = null;
                        Console.WriteLine($"🗑️ White player removed due to timeout: {connectionId}");
                    }

                    if (game.BlackPlayer == player)
                    {
                        game.BlackPlayer = null;
                        Console.WriteLine($"🗑️ Black player removed due to timeout: {connectionId}");
                    }
                    
                    // اگر بازی خالی شد، آن را حذف کن
                    if (game.WhitePlayer == null && game.BlackPlayer == null)
                    {
                        _games.TryRemove(game.RoomId, out _);
                        _playerToGame.TryRemove(connectionId, out _);
                        Console.WriteLine($"🗑️ Game removed (no players): {game.RoomId}");
                    }
                }
            }

            public GameRoom? GetGame(string roomId)
            {
                _games.TryGetValue(roomId, out var room);
                return room;
            }

    public GameRoom? GetGameByInviteCode(string inviteCode)
    {
        if (string.IsNullOrEmpty(inviteCode))
        {
            Console.WriteLine($"❌ GetGameByInviteCode: Empty invite code");
            return null;
        }
        
        try
        {
            Console.WriteLine($"🔍 GetGameByInviteCode: Looking for code '{inviteCode}'");
            Console.WriteLine($"🔍 Total games in memory: {_games.Count}");
            
            foreach (var game in _games.Values)
            {
                Console.WriteLine($"   Game: {game.RoomId}, InviteCode: {game.InviteCode}, Private: {game.IsPrivate}");
            }
            
            var room = _games.Values
                .FirstOrDefault(g => 
                    g.InviteCode != null && 
                    g.InviteCode.Equals(inviteCode, StringComparison.OrdinalIgnoreCase));
            
            if (room != null)
            {
                Console.WriteLine($"✅ GetGameByInviteCode: Found game {room.RoomId}");
            }
            else
            {
                Console.WriteLine($"❌ GetGameByInviteCode: No game found with code '{inviteCode}'");
            }
            
            return room;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 GetGameByInviteCode ERROR: {ex.Message}");
            return null;
        }
    }
            public IEnumerable<GameRoom> GetAllGames()
            {
                return _games.Values;
            }
            
            public GameRoom? JoinGame(string roomId, Player player)
            {
                if (!_games.TryGetValue(roomId, out var room)) 
                {
                    Console.WriteLine($"❌ Game not found: {roomId}");
                    return null;
                }
                
                Console.WriteLine($"🎮 Joining game - Room: {roomId}, Player: {player.Username}, CurrentWhite: {room.WhitePlayer?.Username}, CurrentBlack: {room.BlackPlayer?.Username}");
                
                // اگر بازیکن سفید نباشد و جای سیاه خالی باشد، سیاه می‌شود
                if (room.WhitePlayer == null)
                {
                    room.WhitePlayer = player;
                    Console.WriteLine($"⚪ Assigned as White: {player.Username}");
                }
                else if (room.BlackPlayer == null)
                {
                    room.BlackPlayer = player;
                    Console.WriteLine($"⚫ Assigned as Black: {player.Username}");
                }
                else
                {
                    Console.WriteLine($"❌ Game is full: {roomId}");
                    return null;
                }
                
                // اگر بازی پر شد
                if (room.IsFull && room.Status == GameStatus.WaitingForPlayer)
                {
                    room.Status = GameStatus.InProgress;
                    room.StartedAt = DateTime.UtcNow;
                    Console.WriteLine($"🚀 Game started: {roomId}");
                }
                
                _playerToGame[player.ConnectionId] = roomId;
                return room;
            }
            
            public void RemovePlayer(string connectionId)
            {
                if (_playerToGame.TryRemove(connectionId, out var roomId))
                {
                    if (_games.TryGetValue(roomId, out var room))
                    {
                        if (room.WhitePlayer?.ConnectionId == connectionId)
                        {
                            room.WhitePlayer = null;
                            Console.WriteLine($"👤 White player removed: {connectionId}");
                        }
                        else if (room.BlackPlayer?.ConnectionId == connectionId)
                        {
                            room.BlackPlayer = null;
                            Console.WriteLine($"👤 Black player removed: {connectionId}");
                        }
                        
                        if (!room.IsFull)
                            room.Status = GameStatus.WaitingForPlayer;
                    }
                }
            }
        }
    }
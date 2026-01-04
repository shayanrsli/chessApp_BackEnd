// Services/GameManager.cs
using ChessServer.Models;
using ChessServer.Models.Enums;
using System.Collections.Concurrent;

namespace ChessServer.Services
{
    public class GameManager
    {
        private readonly ConcurrentDictionary<string, GameRoom> _games = new();
        private readonly ConcurrentDictionary<string, string> _playerToGame = new();
        
        public GameRoom CreateGame(string gameName = "Chess Game", bool isPrivate = false)
        {
            var room = new GameRoom
            {
                Name = gameName,
                IsPrivate = isPrivate,
                InviteCode = isPrivate ? Guid.NewGuid().ToString().Substring(0, 8).ToUpper() : null,
                Board = new ChessBoard()  // ✅ ایجاد صفحه شطرنج
            };
            
            Console.WriteLine($"🎮 Creating game: {room.RoomId}, InviteCode: {room.InviteCode}");
            
            _games[room.RoomId] = room;
            return room;
        }
        
        public GameRoom? GetGame(string roomId)
        {
            return _games.TryGetValue(roomId, out var room) ? room : null;
        }
        
        public GameRoom? GetGameByInviteCode(string inviteCode)
        {
            if (string.IsNullOrEmpty(inviteCode))
                return null;
                
            return _games.Values
                .FirstOrDefault(g => g.InviteCode == inviteCode);
        }
        
        public IEnumerable<GameRoom> GetAllGames()
        {
            return _games.Values;
        }
        
        public GameRoom? JoinGame(string roomId, Player player)
        {
            if (!_games.TryGetValue(roomId, out var room)) return null;
            
            if (room.IsFull) return null;
            
            // اگر اولین بازیکن هستی
            if (room.WhitePlayer == null && room.BlackPlayer == null)
            {
                room.WhitePlayer = player;
            }
            else if (room.BlackPlayer == null)
            {
                room.BlackPlayer = player;
            }
            
            // اگر بازی پر شد
            if (room.IsFull)
            {
                room.Status = GameStatus.InProgress;
                room.StartedAt = DateTime.UtcNow;
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
                        room.WhitePlayer = null;
                    else if (room.BlackPlayer?.ConnectionId == connectionId)
                        room.BlackPlayer = null;
                    
                    if (!room.IsFull)
                        room.Status = GameStatus.WaitingForPlayer;
                }
            }
        }
    }
}
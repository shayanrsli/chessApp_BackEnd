using System.Collections.Concurrent;
using ChessServer.Models;

namespace ChessServer.Services
{
    public class GameManager
    {
        private readonly ConcurrentDictionary<string, GameRoom> _games = new();
        private readonly ConcurrentDictionary<string, string> _inviteToRoom = new();
        private readonly ConcurrentDictionary<string, (string RoomId, string UserId)> _connectionIndex = new();
        private readonly ConcurrentDictionary<string, object> _roomLocks = new();

        private object GetRoomLock(string roomId) => _roomLocks.GetOrAdd(roomId, _ => new object());

        public GameRoom CreateGame(string name, bool isPrivate)
        {
            var room = new GameRoom
            {
                RoomId = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(name) ? "Chess Game" : name.Trim(),
                IsPrivate = isPrivate,
                InviteCode = GenerateInviteCode()
            };

            _games[room.RoomId] = room;
            _inviteToRoom[room.InviteCode] = room.RoomId;
            _roomLocks.TryAdd(room.RoomId, new object());
            return room;
        }

        public GameRoom? GetGame(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId)) return null;
            return _games.TryGetValue(roomId, out var room) ? room : null;
        }

        public GameRoom? GetGameByInviteCode(string inviteCode)
        {
            if (string.IsNullOrWhiteSpace(inviteCode)) return null;
            inviteCode = inviteCode.Trim().ToUpperInvariant();

            if (!_inviteToRoom.TryGetValue(inviteCode, out var roomId)) return null;
            return GetGame(roomId);
        }

        public IReadOnlyList<GameRoom> GetAllGames() => _games.Values.ToList();

        public void IndexConnection(string connectionId, string roomId, string userId)
        {
            if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(userId))
                return;

            _connectionIndex[connectionId] = (roomId, userId);
        }

        public (string RoomId, string UserId)? FindByConnection(string connectionId)
        {
            return _connectionIndex.TryGetValue(connectionId, out var v) ? v : null;
        }

        public void MarkPlayerDisconnected(string connectionId)
        {
            var info = FindByConnection(connectionId);
            if (info == null) return;

            var room = GetGame(info.Value.RoomId);
            if (room == null) return;

            lock (GetRoomLock(room.RoomId))
            {
                if (room.WhitePlayer?.ConnectionId == connectionId)
                {
                    room.WhitePlayer.IsConnected = false;
                    room.WhitePlayer.LastSeenAt = DateTime.UtcNow;
                }
                if (room.BlackPlayer?.ConnectionId == connectionId)
                {
                    room.BlackPlayer.IsConnected = false;
                    room.BlackPlayer.LastSeenAt = DateTime.UtcNow;
                }
            }
        }

        public void RemovePlayerIfStillDisconnected(string connectionId, TimeSpan threshold)
        {
            var info = FindByConnection(connectionId);
            if (info == null) return;

            var room = GetGame(info.Value.RoomId);
            if (room == null) return;

            lock (GetRoomLock(room.RoomId))
            {
                if (room.WhitePlayer?.ConnectionId == connectionId && room.WhitePlayer.IsConnected == false)
                {
                    if (DateTime.UtcNow - room.WhitePlayer.LastSeenAt >= threshold)
                        room.WhitePlayer = null;
                }

                if (room.BlackPlayer?.ConnectionId == connectionId && room.BlackPlayer.IsConnected == false)
                {
                    if (DateTime.UtcNow - room.BlackPlayer.LastSeenAt >= threshold)
                        room.BlackPlayer = null;
                }

                if (room.WhitePlayer == null && room.BlackPlayer == null)
                {
                    _games.TryRemove(room.RoomId, out _);
                    _roomLocks.TryRemove(room.RoomId, out _);
                    if (!string.IsNullOrEmpty(room.InviteCode))
                        _inviteToRoom.TryRemove(room.InviteCode, out _);
                }
            }

            _connectionIndex.TryRemove(connectionId, out _);
        }

        private static string GenerateInviteCode()
        {
            // 8-char uppercase
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rnd = Random.Shared;
            return new string(Enumerable.Range(0, 8).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
        }
    }
}

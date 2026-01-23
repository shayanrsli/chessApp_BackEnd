using System;

namespace ChessServer.Models
{
    public class Player
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public bool IsConnected { get; set; } = true;
        public DateTime? DisconnectedAt { get; set; }
        
        // برای تشخیص بازیکن
        public override bool Equals(object? obj)
        {
            if (obj is Player other)
            {
                return ConnectionId == other.ConnectionId;
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            return ConnectionId.GetHashCode();
        }
    }
}
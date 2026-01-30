using System;

namespace ChessServer.Models
{
    public class Player
    {
        public string ConnectionId { get; set; } = "";
        public string UserId { get; set; } = "";
        public string Username { get; set; } = "";
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool IsConnected { get; set; } = true;
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }

}
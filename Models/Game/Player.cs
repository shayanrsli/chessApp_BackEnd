namespace ChessServer.Models
{
    public class Player
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool IsConnected { get; set; } = true;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        
        // برای دعوت از طریق تلگرام
        public string? TelegramId { get; set; }
        public string? InviteCode { get; set; }
    }
}
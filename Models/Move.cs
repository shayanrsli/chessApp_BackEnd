namespace ChessServer.Models
{
    public class Move
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string? Promotion { get; set; }
        public string PlayerConnectionId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? San { get; set; } // حرکت به فرمت استاندارد
    }
}
namespace ChessServer.Models
{
    public class Move
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string? Promotion { get; set; }
        public string PlayerUserId { get; set; } = "";
        public string PlayerConnectionId { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? FenAfter { get; set; } 
    }

}
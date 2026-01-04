namespace ChessServer.Models
{
    public class Move
    {
        public string From { get; set; } = null!;
        public string To { get; set; } = null!;
        public string? Promotion { get; set; }

        public string PlayerConnectionId { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}

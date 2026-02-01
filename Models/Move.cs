namespace ChessServer.Models
{
    public class Move
    {
        public string From { get; set; } = default!;
        public string To { get; set; } = default!;
        public string? Promotion { get; set; }

        public string PlayerUserId { get; set; } = default!;
        public string PlayerConnectionId { get; set; } = default!;

        public DateTime Timestamp { get; set; }

        public string? FenAfter { get; set; }
    }
}

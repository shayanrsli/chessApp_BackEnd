using ChessServer.Models.Enums;

namespace ChessServer.Models
{
    public class GameRoom
    {
        public string RoomId { get; set; } = default!;
        public string Name { get; set; } = "Chess Game";
        public bool IsPrivate { get; set; }
        public string InviteCode { get; set; } = default!;

        public Player? WhitePlayer { get; set; }
        public Player? BlackPlayer { get; set; }

        public GameStatus Status { get; set; } = GameStatus.WaitingForPlayer;

        public string CurrentFen { get; set; } = ChessBoard.InitialFen;

        public List<Move> Moves { get; set; } = new();

        public DateTime? StartedAt { get; set; }

        // ✅ Clock (Server-authoritative)
        public int InitialSeconds { get; set; } = 300;
        public int IncrementSeconds { get; set; } = 0;

        public int WhiteTimeLeft { get; set; } = 300;
        public int BlackTimeLeft { get; set; } = 300;

        public string ActiveColor { get; set; } = "white"; // "white" | "black"
        public DateTime LastTickAtUtc { get; set; } = DateTime.UtcNow;

        public bool IsFull => WhitePlayer != null && BlackPlayer != null;
    }
}

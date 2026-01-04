using ChessServer.Models.Enums;

namespace ChessServer.Models
{
    public class GameRoom
    {
        public string RoomId { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Chess Game";
        public bool IsPrivate { get; set; }
        public string? InviteCode { get; set; }

        public string CurrentFen { get; set; } = "startpos";


        public GameStatus Status { get; set; } = GameStatus.WaitingForPlayer;

        public Player? WhitePlayer { get; set; }
        public Player? BlackPlayer { get; set; }

        public ChessBoard Board { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }

        // ✅⬇️ اینو اضافه کن
        public List<Move> Moves { get; set; } = new();

        public bool IsFull => WhitePlayer != null && BlackPlayer != null;
    }
}

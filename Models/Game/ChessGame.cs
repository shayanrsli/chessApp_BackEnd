// Models/ChessBoard.cs
using System.Text;

namespace ChessServer.Models
{
    public class ChessBoard
    {
        private string[,] board;
        
                public const string InitialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public ChessBoard()
        {
            InitializeBoard();
        }
        
        private void InitializeBoard()
        {
            board = new string[8, 8];
            
            // سفید
            board[7, 0] = "R"; board[7, 1] = "N"; board[7, 2] = "B"; board[7, 3] = "Q";
            board[7, 4] = "K"; board[7, 5] = "B"; board[7, 6] = "N"; board[7, 7] = "R";
            for (int i = 0; i < 8; i++) board[6, i] = "P";
            
            // سیاه
            board[0, 0] = "r"; board[0, 1] = "n"; board[0, 2] = "b"; board[0, 3] = "q";
            board[0, 4] = "k"; board[0, 5] = "b"; board[0, 6] = "n"; board[0, 7] = "r";
            for (int i = 0; i < 8; i++) board[1, i] = "p";
            
            // خالی
            for (int i = 2; i < 6; i++)
                for (int j = 0; j < 8; j++)
                    board[i, j] = null;
        }
        
        public string GetCurrentFen()
        {
            return InitialFen;
        }
        
        public string[,] GetCurrentBoard()
        {
            return board;
        }
        
        public object GetBoardState()
        {
            var fen = new StringBuilder();
            
            for (int row = 0; row < 8; row++)
            {
                int emptyCount = 0;
                
                for (int col = 0; col < 8; col++)
                {
                    var piece = board[row, col];
                    
                    if (piece == null)
                    {
                        emptyCount++;
                    }
                    else
                    {
                        if (emptyCount > 0)
                        {
                            fen.Append(emptyCount);
                            emptyCount = 0;
                        }
                        fen.Append(piece);
                    }
                }
                
                if (emptyCount > 0)
                {
                    fen.Append(emptyCount);
                }
                
                if (row < 7)
                {
                    fen.Append('/');
                }
            }
            
            // قسمت‌های دیگر FEN (فعلاً ساده)
            fen.Append(" w KQkq - 0 1");
            
            return new
            {
                Fen = fen.ToString(),
                Board = board,
                CurrentTurn = "white",
                MoveNumber = 1
            };
        }
    }
}
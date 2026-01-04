// Models/ChessBoard.cs
namespace ChessServer.Models
{
    public class ChessBoard
    {
        private string[,] board = new string[8, 8];
        
        public ChessBoard()
        {
            InitializeBoard();
        }
        
        private void InitializeBoard()
        {
            // تنظیم مهره‌های سفید
            board[0, 0] = "♜"; board[0, 1] = "♞"; board[0, 2] = "♝"; board[0, 3] = "♛"; 
            board[0, 4] = "♚"; board[0, 5] = "♝"; board[0, 6] = "♞"; board[0, 7] = "♜";
            for (int i = 0; i < 8; i++) board[1, i] = "♟";
            
            // خانه‌های خالی
            for (int i = 2; i < 6; i++)
                for (int j = 0; j < 8; j++)
                    board[i, j] = "";
            
            // تنظیم مهره‌های سیاه
            for (int i = 0; i < 8; i++) board[6, i] = "♙";
            board[7, 0] = "♖"; board[7, 1] = "♘"; board[7, 2] = "♗"; board[7, 3] = "♕";
            board[7, 4] = "♔"; board[7, 5] = "♗"; board[7, 6] = "♘"; board[7, 7] = "♖";
        }
        
        public object[,] GetCurrentBoard()
        {
            var result = new object[8, 8];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    result[i, j] = new
                    {
                        piece = board[i, j],
                        row = i,
                        col = j,
                        color = string.IsNullOrEmpty(board[i, j]) ? "" : 
                               "♙♖♘♗♕♔".Contains(board[i, j]) ? "black" : "white"
                    };
                }
            }
            return result;
        }
        
        public bool MakeMove(string from, string to, string? promotion = null)
        {
            // در اینجا منطق حرکت رو اضافه کن
            // فعلاً برای تست، حرکت رو قبول می‌کنه
            return true;
        }
    }
}
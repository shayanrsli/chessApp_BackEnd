namespace ChessServer.Models.Enums
{

    public enum GameStatus
    {
        WaitingForPlayer = 0,
        InProgress = 1,
        Finished = 2,  // ✅ این رو اضافه کردم
        Draw = 3,
        WhiteWon = 4,
        BlackWon = 5,
        Abandoned = 6
    }

    public enum PlayerColor
    {
        White,
        Black,
        Random
    }
}
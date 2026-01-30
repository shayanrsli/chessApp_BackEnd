namespace ChessServer.Models.Enums
{
    public enum GameStatus
    {
        WaitingForPlayer = 0,
        InProgress = 1,
        Finished = 2
    }

    public enum PlayerColor
    {
        White,
        Black,
        Random
    }
}
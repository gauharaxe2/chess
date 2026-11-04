namespace ChessMobile.Core
{
    public enum PieceType : byte
    {
        None = 0,
        Pawn = 1,
        Knight = 2,
        Bishop = 3,
        Rook = 4,
        Queen = 5,
        King = 6
    }

    public enum PieceColor : byte
    {
        White = 0,
        Black = 1
    }

    public enum GameResult : byte
    {
        None = 0,
        WhiteWins = 1,
        BlackWins = 2,
        Draw = 3
    }

    public enum DrawReason : byte
    {
        None = 0,
        Stalemate = 1,
        InsufficientMaterial = 2,
        ThreefoldRepetition = 3,
        FiftyMoveRule = 4,
        Agreement = 5
    }
}

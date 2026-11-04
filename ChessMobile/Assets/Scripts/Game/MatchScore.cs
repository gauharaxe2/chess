using ChessMobile.Core;

namespace ChessMobile.Game
{
    /// <summary>Match is won by the first player to deliver <see cref="CheckmatesToWin"/> checkmates.</summary>
    public sealed class MatchScore
    {
        public const int CheckmatesToWin = 2;

        public int WhiteCheckmates { get; private set; }
        public int BlackCheckmates { get; private set; }
        public GameResult MatchWinner { get; private set; } = GameResult.None;
        public bool IsMatchOver => MatchWinner != GameResult.None;

        public void Reset()
        {
            WhiteCheckmates = 0;
            BlackCheckmates = 0;
            MatchWinner = GameResult.None;
        }

        /// <summary>
        /// Applies a finished game result. Only checkmates award a point; draws do not.
        /// Returns true if this result decided the match.
        /// </summary>
        public bool ApplyGameResult(GameResult result)
        {
            if (IsMatchOver || result == GameResult.None || result == GameResult.Draw)
                return false;

            if (result == GameResult.WhiteWins)
                WhiteCheckmates++;
            else if (result == GameResult.BlackWins)
                BlackCheckmates++;

            if (WhiteCheckmates >= CheckmatesToWin)
            {
                MatchWinner = GameResult.WhiteWins;
                return true;
            }

            if (BlackCheckmates >= CheckmatesToWin)
            {
                MatchWinner = GameResult.BlackWins;
                return true;
            }

            return false;
        }

        public string ScoreLine() =>
            $"Match {WhiteCheckmates}–{BlackCheckmates} (first to {CheckmatesToWin} checkmates)";
    }
}

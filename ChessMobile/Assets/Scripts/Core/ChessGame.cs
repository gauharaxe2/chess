using System;
using System.Collections.Generic;

namespace ChessMobile.Core
{
    public sealed class ChessGame
    {
        public BoardState Board { get; private set; }
        public GameResult Result { get; private set; } = GameResult.None;
        public DrawReason DrawReason { get; private set; } = DrawReason.None;
        public IReadOnlyList<Move> MoveHistory => _history;
        public event Action OnBoardChanged;
        public event Action<GameResult, DrawReason> OnGameEnded;

        private readonly List<Move> _history = new List<Move>();
        private readonly List<string> _positionHistory = new List<string>();

        public ChessGame()
        {
            Board = BoardState.CreateInitial();
            _positionHistory.Add(Board.PositionKey());
        }

        public ChessGame(string fen)
        {
            Board = BoardState.FromFen(fen);
            _positionHistory.Add(Board.PositionKey());
        }

        public void Reset()
        {
            Board = BoardState.CreateInitial();
            _history.Clear();
            _positionHistory.Clear();
            _positionHistory.Add(Board.PositionKey());
            Result = GameResult.None;
            DrawReason = DrawReason.None;
            OnBoardChanged?.Invoke();
        }

        public List<Move> GetLegalMoves() => MoveGenerator.GenerateLegalMoves(Board);

        public List<Move> GetLegalMovesFrom(Square from) => MoveGenerator.GenerateLegalMovesFrom(Board, from);

        public bool TryMakeMove(Move move)
        {
            if (Result != GameResult.None)
                return false;

            var legal = MoveGenerator.GenerateLegalMoves(Board);
            bool found = false;
            foreach (var m in legal)
            {
                if (m.Equals(move))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;

            MoveGenerator.ApplyMove(Board, move);
            _history.Add(move);
            _positionHistory.Add(Board.PositionKey());
            EvaluateTerminalState();
            OnBoardChanged?.Invoke();
            return true;
        }

        public bool TryMakeMoveUci(string uci)
        {
            try
            {
                var move = Move.FromUci(uci, (from, to, promo) =>
                    MoveGenerator.ResolveUci(Board, from, to, promo));
                return TryMakeMove(move);
            }
            catch
            {
                return false;
            }
        }

        public bool IsInCheck => MoveGenerator.IsInCheck(Board, Board.SideToMove);

        private void EvaluateTerminalState()
        {
            var legal = MoveGenerator.GenerateLegalMoves(Board);
            if (legal.Count == 0)
            {
                if (MoveGenerator.IsInCheck(Board, Board.SideToMove))
                {
                    Result = Board.SideToMove == PieceColor.White ? GameResult.BlackWins : GameResult.WhiteWins;
                    DrawReason = DrawReason.None;
                }
                else
                {
                    Result = GameResult.Draw;
                    DrawReason = DrawReason.Stalemate;
                }

                OnGameEnded?.Invoke(Result, DrawReason);
                return;
            }

            if (Board.HalfMoveClock >= 100)
            {
                EndDraw(DrawReason.FiftyMoveRule);
                return;
            }

            if (IsInsufficientMaterial())
            {
                EndDraw(DrawReason.InsufficientMaterial);
                return;
            }

            if (IsThreefold())
            {
                EndDraw(DrawReason.ThreefoldRepetition);
            }
        }

        private void EndDraw(DrawReason reason)
        {
            Result = GameResult.Draw;
            DrawReason = reason;
            OnGameEnded?.Invoke(Result, DrawReason);
        }

        private bool IsThreefold()
        {
            string current = Board.PositionKey();
            int count = 0;
            foreach (var key in _positionHistory)
            {
                if (key == current)
                    count++;
            }

            return count >= 3;
        }

        private bool IsInsufficientMaterial()
        {
            int whiteMinors = 0, blackMinors = 0;
            bool whiteBishop = false, blackBishop = false;
            bool whiteKnight = false, blackKnight = false;

            for (int i = 0; i < 64; i++)
            {
                var p = Board[Square.FromIndex(i)];
                if (p.IsEmpty || p.Type == PieceType.King) continue;
                if (p.Type == PieceType.Pawn || p.Type == PieceType.Rook || p.Type == PieceType.Queen)
                    return false;

                if (p.Color == PieceColor.White)
                {
                    whiteMinors++;
                    if (p.Type == PieceType.Bishop) whiteBishop = true;
                    if (p.Type == PieceType.Knight) whiteKnight = true;
                }
                else
                {
                    blackMinors++;
                    if (p.Type == PieceType.Bishop) blackBishop = true;
                    if (p.Type == PieceType.Knight) blackKnight = true;
                }
            }

            // K vs K
            if (whiteMinors == 0 && blackMinors == 0) return true;
            // K+B vs K or K+N vs K
            if (whiteMinors + blackMinors == 1) return true;
            // K+B vs K+B same color is rare; treat K+minor vs K+minor as playable except bare bishops of same color skipped for simplicity
            if (whiteMinors == 1 && blackMinors == 1 && whiteBishop && blackBishop && !whiteKnight && !blackKnight)
                return true;

            return false;
        }

        public string ToFen() => Board.ToFen();
    }
}

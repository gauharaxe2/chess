using System;
using System.Text;

namespace ChessMobile.Core
{
    /// <summary>Mutable chess position with FEN support and castling/en-passant state.</summary>
    public sealed class BoardState
    {
        private readonly Piece[] _squares = new Piece[64];

        public PieceColor SideToMove { get; set; } = PieceColor.White;
        public bool WhiteCastleKingSide { get; set; } = true;
        public bool WhiteCastleQueenSide { get; set; } = true;
        public bool BlackCastleKingSide { get; set; } = true;
        public bool BlackCastleQueenSide { get; set; } = true;
        public Square? EnPassantTarget { get; set; }
        public int HalfMoveClock { get; set; }
        public int FullMoveNumber { get; set; } = 1;

        public Piece this[Square square]
        {
            get => _squares[square.Index];
            set => _squares[square.Index] = value;
        }

        public Piece this[int file, int rank]
        {
            get => _squares[rank * 8 + file];
            set => _squares[rank * 8 + file] = value;
        }

        public static BoardState CreateInitial()
        {
            var board = new BoardState();
            board.SetupStartingPosition();
            return board;
        }

        public void Clear()
        {
            for (int i = 0; i < 64; i++)
                _squares[i] = Piece.Empty;

            SideToMove = PieceColor.White;
            WhiteCastleKingSide = WhiteCastleQueenSide = true;
            BlackCastleKingSide = BlackCastleQueenSide = true;
            EnPassantTarget = null;
            HalfMoveClock = 0;
            FullMoveNumber = 1;
        }

        public void SetupStartingPosition()
        {
            Clear();
            PlaceBackRank(0, PieceColor.White);
            PlaceBackRank(7, PieceColor.Black);

            for (int file = 0; file < 8; file++)
            {
                this[file, 1] = new Piece(PieceType.Pawn, PieceColor.White);
                this[file, 6] = new Piece(PieceType.Pawn, PieceColor.Black);
            }
        }

        private void PlaceBackRank(int rank, PieceColor color)
        {
            PieceType[] order =
            {
                PieceType.Rook, PieceType.Knight, PieceType.Bishop, PieceType.Queen,
                PieceType.King, PieceType.Bishop, PieceType.Knight, PieceType.Rook
            };

            for (int file = 0; file < 8; file++)
                this[file, rank] = new Piece(order[file], color);
        }

        public BoardState Clone()
        {
            var clone = new BoardState
            {
                SideToMove = SideToMove,
                WhiteCastleKingSide = WhiteCastleKingSide,
                WhiteCastleQueenSide = WhiteCastleQueenSide,
                BlackCastleKingSide = BlackCastleKingSide,
                BlackCastleQueenSide = BlackCastleQueenSide,
                EnPassantTarget = EnPassantTarget,
                HalfMoveClock = HalfMoveClock,
                FullMoveNumber = FullMoveNumber
            };
            Array.Copy(_squares, clone._squares, 64);
            return clone;
        }

        public Square FindKing(PieceColor color)
        {
            for (int i = 0; i < 64; i++)
            {
                var piece = _squares[i];
                if (piece.Type == PieceType.King && piece.Color == color)
                    return Square.FromIndex(i);
            }

            throw new InvalidOperationException("King not found for " + color);
        }

        public string ToFen()
        {
            var sb = new StringBuilder(80);
            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    var piece = this[file, rank];
                    if (piece.IsEmpty)
                    {
                        empty++;
                        continue;
                    }

                    if (empty > 0)
                    {
                        sb.Append(empty);
                        empty = 0;
                    }

                    sb.Append(piece.ToFenChar());
                }

                if (empty > 0) sb.Append(empty);
                if (rank > 0) sb.Append('/');
            }

            sb.Append(SideToMove == PieceColor.White ? " w " : " b ");

            bool anyCastle = false;
            if (WhiteCastleKingSide) { sb.Append('K'); anyCastle = true; }
            if (WhiteCastleQueenSide) { sb.Append('Q'); anyCastle = true; }
            if (BlackCastleKingSide) { sb.Append('k'); anyCastle = true; }
            if (BlackCastleQueenSide) { sb.Append('q'); anyCastle = true; }
            if (!anyCastle) sb.Append('-');

            sb.Append(' ');
            sb.Append(EnPassantTarget.HasValue ? EnPassantTarget.Value.ToAlgebraic() : "-");
            sb.Append(' ');
            sb.Append(HalfMoveClock);
            sb.Append(' ');
            sb.Append(FullMoveNumber);
            return sb.ToString();
        }

        public static BoardState FromFen(string fen)
        {
            if (string.IsNullOrWhiteSpace(fen))
                throw new ArgumentException("FEN is empty");

            var parts = fen.Trim().Split(' ');
            if (parts.Length < 4)
                throw new ArgumentException("Invalid FEN: " + fen);

            var board = new BoardState();
            board.Clear();

            var ranks = parts[0].Split('/');
            if (ranks.Length != 8)
                throw new ArgumentException("Invalid FEN ranks");

            for (int r = 0; r < 8; r++)
            {
                int rank = 7 - r;
                int file = 0;
                foreach (char c in ranks[r])
                {
                    if (char.IsDigit(c))
                    {
                        file += c - '0';
                        continue;
                    }

                    board[file, rank] = Piece.FromFenChar(c);
                    file++;
                }
            }

            board.SideToMove = parts[1] == "b" ? PieceColor.Black : PieceColor.White;

            board.WhiteCastleKingSide = parts[2].Contains("K");
            board.WhiteCastleQueenSide = parts[2].Contains("Q");
            board.BlackCastleKingSide = parts[2].Contains("k");
            board.BlackCastleQueenSide = parts[2].Contains("q");

            board.EnPassantTarget = parts[3] == "-" ? (Square?)null : Square.FromAlgebraic(parts[3]);

            if (parts.Length > 4 && int.TryParse(parts[4], out int half))
                board.HalfMoveClock = half;
            if (parts.Length > 5 && int.TryParse(parts[5], out int full))
                board.FullMoveNumber = full;

            return board;
        }

        public string PositionKey()
        {
            // Used for threefold repetition (pieces + side + castling + ep).
            var sb = new StringBuilder(80);
            for (int i = 0; i < 64; i++)
                sb.Append(_squares[i].ToFenChar());
            sb.Append(SideToMove == PieceColor.White ? 'w' : 'b');
            sb.Append(WhiteCastleKingSide ? 'K' : '-');
            sb.Append(WhiteCastleQueenSide ? 'Q' : '-');
            sb.Append(BlackCastleKingSide ? 'k' : '-');
            sb.Append(BlackCastleQueenSide ? 'q' : '-');
            sb.Append(EnPassantTarget.HasValue ? EnPassantTarget.Value.ToAlgebraic() : "-");
            return sb.ToString();
        }
    }
}

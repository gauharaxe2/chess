using System;

namespace ChessMobile.Core
{
    [Flags]
    public enum MoveFlags : byte
    {
        None = 0,
        Capture = 1,
        DoublePawnPush = 2,
        EnPassant = 4,
        CastleKingSide = 8,
        CastleQueenSide = 16,
        Promotion = 32
    }

    [Serializable]
    public readonly struct Move : IEquatable<Move>
    {
        public readonly Square From;
        public readonly Square To;
        public readonly MoveFlags Flags;
        public readonly PieceType PromotionPiece;

        public Move(Square from, Square to, MoveFlags flags = MoveFlags.None, PieceType promotion = PieceType.None)
        {
            From = from;
            To = to;
            Flags = flags;
            PromotionPiece = promotion;
        }

        public bool IsPromotion => (Flags & MoveFlags.Promotion) != 0;
        public bool IsCapture => (Flags & MoveFlags.Capture) != 0 || (Flags & MoveFlags.EnPassant) != 0;
        public bool IsCastle => (Flags & (MoveFlags.CastleKingSide | MoveFlags.CastleQueenSide)) != 0;

        public string ToUci()
        {
            string baseMove = From.ToAlgebraic() + To.ToAlgebraic();
            if (!IsPromotion) return baseMove;

            char promo = PromotionPiece switch
            {
                PieceType.Queen => 'q',
                PieceType.Rook => 'r',
                PieceType.Bishop => 'b',
                PieceType.Knight => 'n',
                _ => 'q'
            };
            return baseMove + promo;
        }

        public static Move FromUci(string uci, Func<Square, Square, PieceType, Move> resolver)
        {
            if (string.IsNullOrEmpty(uci) || uci.Length < 4)
                throw new ArgumentException("Invalid UCI move: " + uci);

            var from = Square.FromAlgebraic(uci.Substring(0, 2));
            var to = Square.FromAlgebraic(uci.Substring(2, 2));
            PieceType promo = PieceType.None;
            if (uci.Length >= 5)
            {
                promo = char.ToLowerInvariant(uci[4]) switch
                {
                    'q' => PieceType.Queen,
                    'r' => PieceType.Rook,
                    'b' => PieceType.Bishop,
                    'n' => PieceType.Knight,
                    _ => PieceType.Queen
                };
            }

            return resolver(from, to, promo);
        }

        public bool Equals(Move other) =>
            From == other.From && To == other.To && Flags == other.Flags && PromotionPiece == other.PromotionPiece;

        public override bool Equals(object obj) => obj is Move other && Equals(other);
        public override int GetHashCode() =>
            (From.Index * 397) ^ (To.Index * 31) ^ ((int)Flags << 8) ^ (int)PromotionPiece;
        public override string ToString() => ToUci();
    }
}

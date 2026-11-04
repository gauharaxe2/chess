using System;

namespace ChessMobile.Core
{
    [Serializable]
    public readonly struct Piece : IEquatable<Piece>
    {
        public readonly PieceType Type;
        public readonly PieceColor Color;

        public static readonly Piece Empty = new Piece(PieceType.None, PieceColor.White);

        public Piece(PieceType type, PieceColor color)
        {
            Type = type;
            Color = color;
        }

        public bool IsEmpty => Type == PieceType.None;

        public PieceColor OppositeColor => Color == PieceColor.White ? PieceColor.Black : PieceColor.White;

        public char ToFenChar()
        {
            char c = Type switch
            {
                PieceType.Pawn => 'p',
                PieceType.Knight => 'n',
                PieceType.Bishop => 'b',
                PieceType.Rook => 'r',
                PieceType.Queen => 'q',
                PieceType.King => 'k',
                _ => ' '
            };
            return Color == PieceColor.White ? char.ToUpperInvariant(c) : c;
        }

        public static Piece FromFenChar(char c)
        {
            var color = char.IsUpper(c) ? PieceColor.White : PieceColor.Black;
            var type = char.ToLowerInvariant(c) switch
            {
                'p' => PieceType.Pawn,
                'n' => PieceType.Knight,
                'b' => PieceType.Bishop,
                'r' => PieceType.Rook,
                'q' => PieceType.Queen,
                'k' => PieceType.King,
                _ => PieceType.None
            };
            return type == PieceType.None ? Empty : new Piece(type, color);
        }

        public bool Equals(Piece other) => Type == other.Type && Color == other.Color;
        public override bool Equals(object obj) => obj is Piece other && Equals(other);
        public override int GetHashCode() => ((int)Type << 1) | (int)Color;
        public override string ToString() => IsEmpty ? "." : ToFenChar().ToString();
    }
}

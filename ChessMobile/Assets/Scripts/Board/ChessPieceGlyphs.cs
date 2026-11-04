using ChessMobile.Core;

namespace ChessMobile.Board
{
    public static class ChessPieceGlyphs
    {
        public static string Get(Piece piece)
        {
            if (piece.IsEmpty) return string.Empty;

            // Unicode chess symbols — crisp on mobile TextMeshPro with a font that includes them.
            // Fallback ASCII letters are used if the font lacks glyphs (see PieceView).
            return piece.Color == PieceColor.White
                ? piece.Type switch
                {
                    PieceType.King => "♔",
                    PieceType.Queen => "♕",
                    PieceType.Rook => "♖",
                    PieceType.Bishop => "♗",
                    PieceType.Knight => "♘",
                    PieceType.Pawn => "♙",
                    _ => ""
                }
                : piece.Type switch
                {
                    PieceType.King => "♚",
                    PieceType.Queen => "♛",
                    PieceType.Rook => "♜",
                    PieceType.Bishop => "♝",
                    PieceType.Knight => "♞",
                    PieceType.Pawn => "♟",
                    _ => ""
                };
        }

        public static string GetAscii(Piece piece)
        {
            if (piece.IsEmpty) return string.Empty;
            char c = piece.Type switch
            {
                PieceType.King => 'K',
                PieceType.Queen => 'Q',
                PieceType.Rook => 'R',
                PieceType.Bishop => 'B',
                PieceType.Knight => 'N',
                PieceType.Pawn => 'P',
                _ => '?'
            };
            return piece.Color == PieceColor.White ? c.ToString() : char.ToLowerInvariant(c).ToString();
        }
    }
}

using ChessMobile.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ChessMobile.Board
{
    public sealed class SquareView : MonoBehaviour
    {
        private Square _square;
        private Image _background;
        private Text _label;
        private Color _baseColor;

        public Square Square => _square;

        public void Setup(Square square, Image background, Text label, Color baseColor)
        {
            _square = square;
            _background = background;
            _label = label;
            _baseColor = baseColor;
        }

        public void SetPiece(Piece piece)
        {
            if (piece.IsEmpty)
            {
                _label.text = string.Empty;
                return;
            }

            string glyph = ChessPieceGlyphs.Get(piece);
            // Prefer unicode; if font renders tofu, user can swap font in Inspector.
            _label.text = string.IsNullOrEmpty(glyph) ? ChessPieceGlyphs.GetAscii(piece) : glyph;
            _label.color = piece.Color == PieceColor.White
                ? new Color(0.98f, 0.98f, 0.95f)
                : new Color(0.12f, 0.10f, 0.10f);
            // Outline-ish contrast via shadow would need another component; keep simple.
        }

        public void SetHighlight(Color color)
        {
            _background.color = Color.Lerp(_baseColor, color, 0.65f);
        }

        public void ResetHighlight()
        {
            _background.color = _baseColor;
        }
    }
}

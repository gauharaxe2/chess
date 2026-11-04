using System.Collections.Generic;
using ChessMobile.Core;
using ChessMobile.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ChessMobile.Board
{
    /// <summary>
    /// Builds an 8x8 interactive board in uGUI and drives selection / move highlighting.
    /// </summary>
    public sealed class ChessBoardView : MonoBehaviour
    {
        [SerializeField] private RectTransform boardRoot;
        [SerializeField] private Color lightSquare = new Color(0.93f, 0.85f, 0.70f);
        [SerializeField] private Color darkSquare = new Color(0.55f, 0.38f, 0.24f);
        [SerializeField] private Color selectedColor = new Color(0.95f, 0.80f, 0.25f, 0.85f);
        [SerializeField] private Color legalColor = new Color(0.35f, 0.75f, 0.40f, 0.70f);
        [SerializeField] private Color checkColor = new Color(0.90f, 0.25f, 0.25f, 0.75f);
        [SerializeField] private Font pieceFont;

        private readonly SquareView[] _squares = new SquareView[64];
        private Square? _selected;
        private List<Move> _legalFromSelected = new List<Move>();
        private bool _flipForBlack;

        public void Initialize(RectTransform root, Font font = null)
        {
            boardRoot = root;
            if (font != null) pieceFont = font;
            BuildBoard();
            if (GameSession.Instance != null)
            {
                GameSession.Instance.OnStateChanged += Refresh;
                Refresh();
            }
        }

        private void OnDestroy()
        {
            if (GameSession.Instance != null)
                GameSession.Instance.OnStateChanged -= Refresh;
        }

        private void BuildBoard()
        {
            for (int i = boardRoot.childCount - 1; i >= 0; i--)
                Destroy(boardRoot.GetChild(i).gameObject);

            var grid = boardRoot.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = boardRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 8;
            grid.spacing = Vector2.zero;
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.cellSize = ComputeCellSize();
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.MiddleCenter;

            // Visual order: rank 8 at top → rank 1 at bottom (standard diagram).
            for (int rank = 7; rank >= 0; rank--)
            {
                for (int file = 0; file < 8; file++)
                {
                    var square = new Square(file, rank);
                    var go = new GameObject($"Sq_{square.ToAlgebraic()}");
                    go.transform.SetParent(boardRoot, false);

                    var image = go.AddComponent<Image>();
                    bool light = (file + rank) % 2 == 1;
                    image.color = light ? lightSquare : darkSquare;

                    var button = go.AddComponent<Button>();
                    button.targetGraphic = image;
                    button.transition = Selectable.Transition.None;

                    var labelGo = new GameObject("Piece");
                    labelGo.transform.SetParent(go.transform, false);
                    var label = labelGo.AddComponent<Text>();
                    label.font = pieceFont != null ? pieceFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
                    label.alignment = TextAnchor.MiddleCenter;
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 12;
                    label.resizeTextMaxSize = 96;
                    label.raycastTarget = false;
                    var labelRt = label.GetComponent<RectTransform>();
                    labelRt.anchorMin = Vector2.zero;
                    labelRt.anchorMax = Vector2.one;
                    labelRt.offsetMin = Vector2.zero;
                    labelRt.offsetMax = Vector2.zero;

                    var view = go.AddComponent<SquareView>();
                    view.Setup(square, image, label, light ? lightSquare : darkSquare);
                    int capturedFile = file;
                    int capturedRank = rank;
                    button.onClick.AddListener(() => OnSquareClicked(new Square(capturedFile, capturedRank)));

                    _squares[square.Index] = view;
                }
            }
        }

        private Vector2 ComputeCellSize()
        {
            var rect = boardRoot.rect;
            float side = Mathf.Min(rect.width, rect.height);
            if (side <= 1f) side = Mathf.Min(Screen.width, Screen.height) * 0.9f;
            float cell = side / 8f;
            return new Vector2(cell, cell);
        }

        private void OnSquareClicked(Square square)
        {
            var session = GameSession.Instance;
            if (session == null || !session.CanInteract)
                return;

            // Flip mapping when local player is Black online.
            Square logical = MapInputSquare(square);

            if (_selected.HasValue)
            {
                Move chosen = default;
                bool found = false;
                foreach (var move in _legalFromSelected)
                {
                    if (move.To == logical)
                    {
                        // Auto-queen for simplicity; promotion UI can replace this later.
                        if (move.IsPromotion && move.PromotionPiece != PieceType.Queen)
                            continue;
                        chosen = move;
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    session.TryPlayMove(chosen);
                    ClearSelection();
                    return;
                }

                // Re-select if clicking another own piece.
                var piece = session.Chess.Board[logical];
                if (!piece.IsEmpty && piece.Color == session.Chess.Board.SideToMove)
                {
                    Select(logical);
                    return;
                }

                ClearSelection();
                return;
            }

            var clicked = session.Chess.Board[logical];
            if (!clicked.IsEmpty && clicked.Color == session.Chess.Board.SideToMove)
                Select(logical);
        }

        private void Select(Square logical)
        {
            _selected = logical;
            _legalFromSelected = GameSession.Instance.Chess.GetLegalMovesFrom(logical);
            RefreshHighlights();
        }

        private void ClearSelection()
        {
            _selected = null;
            _legalFromSelected = new List<Move>();
            RefreshHighlights();
        }

        public void Refresh()
        {
            var session = GameSession.Instance;
            if (session == null) return;

            _flipForBlack = session.IsOnline && session.LocalPlayerColor == PieceColor.Black;
            var board = session.Chess.Board;

            for (int i = 0; i < 64; i++)
            {
                var logical = Square.FromIndex(i);
                var visual = MapVisualSquare(logical);
                var piece = board[logical];
                _squares[visual.Index].SetPiece(piece);
            }

            RefreshHighlights();

            var grid = boardRoot.GetComponent<GridLayoutGroup>();
            if (grid != null)
                grid.cellSize = ComputeCellSize();
        }

        private void RefreshHighlights()
        {
            var session = GameSession.Instance;
            Square? checkSquare = null;
            if (session != null && session.Chess.IsInCheck)
                checkSquare = session.Chess.Board.FindKing(session.Chess.Board.SideToMove);

            for (int i = 0; i < 64; i++)
                _squares[i].ResetHighlight();

            if (checkSquare.HasValue)
            {
                var vis = MapVisualSquare(checkSquare.Value);
                _squares[vis.Index].SetHighlight(checkColor);
            }

            if (_selected.HasValue)
            {
                var vis = MapVisualSquare(_selected.Value);
                _squares[vis.Index].SetHighlight(selectedColor);
                foreach (var move in _legalFromSelected)
                {
                    var toVis = MapVisualSquare(move.To);
                    _squares[toVis.Index].SetHighlight(legalColor);
                }
            }
        }

        private Square MapVisualSquare(Square logical)
        {
            if (!_flipForBlack) return logical;
            return new Square(7 - logical.File, 7 - logical.Rank);
        }

        private Square MapInputSquare(Square visual)
        {
            if (!_flipForBlack) return visual;
            return new Square(7 - visual.File, 7 - visual.Rank);
        }
    }
}

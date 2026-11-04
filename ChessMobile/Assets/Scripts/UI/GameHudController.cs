using ChessMobile.Board;
using ChessMobile.Core;
using ChessMobile.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ChessMobile.UI
{
    public sealed class GameHudController : MonoBehaviour
    {
        private GameObject _root;
        private Text _status;
        private Text _turn;
        private ChessBoardView _boardView;

        public void Build(Transform canvas)
        {
            _root = new GameObject("GameHud");
            _root.transform.SetParent(canvas, false);
            UiFactory.Stretch(_root.AddComponent<RectTransform>());
            _root.SetActive(false);

            UiFactory.CreatePanel(_root.transform, "Backdrop", GameTheme.BgDeep);

            _turn = UiFactory.CreateText(_root.transform, "Turn", "White to move", 34, GameTheme.TextPrimary);
            UiFactory.Anchor(_turn.rectTransform, new Vector2(0.06f, 0.90f), new Vector2(0.70f, 0.97f), Vector2.zero, Vector2.zero);
            _turn.alignment = TextAnchor.MiddleLeft;
            _turn.fontStyle = FontStyle.Bold;

            var menuBtn = UiFactory.CreateButton(_root.transform, "MenuBtn", "Menu", GameTheme.AccentDark, () =>
            {
                Hide();
                FindFirstObjectByType<MainMenuController>()?.Show();
            });
            UiFactory.Anchor(menuBtn.GetComponent<RectTransform>(),
                new Vector2(0.72f, 0.90f), new Vector2(0.96f, 0.97f), Vector2.zero, Vector2.zero);
            menuBtn.GetComponentInChildren<Text>().fontSize = 28;

            var boardHost = new GameObject("BoardHost");
            boardHost.transform.SetParent(_root.transform, false);
            var boardRt = boardHost.AddComponent<RectTransform>();
            UiFactory.Anchor(boardRt, new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.86f), Vector2.zero, Vector2.zero);

            // Square board area centered
            var boardGo = new GameObject("Board");
            boardGo.transform.SetParent(boardHost.transform, false);
            var innerRt = boardGo.AddComponent<RectTransform>();
            innerRt.anchorMin = new Vector2(0.5f, 0.5f);
            innerRt.anchorMax = new Vector2(0.5f, 0.5f);
            float side = Mathf.Min(Screen.width, Screen.height) * 0.88f;
            innerRt.sizeDelta = new Vector2(side, side);

            _boardView = boardGo.AddComponent<ChessBoardView>();
            _boardView.Initialize(innerRt);

            _status = UiFactory.CreateText(_root.transform, "Status", "Ready", 28, GameTheme.TextMuted);
            UiFactory.Anchor(_status.rectTransform, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.20f), Vector2.zero, Vector2.zero);

            var resetBtn = UiFactory.CreateButton(_root.transform, "ResetBtn", "New Game", GameTheme.AccentDark, () =>
            {
                var session = GameSession.Instance;
                if (session == null) return;
                if (session.Mode == GameMode.OfflineHotseat)
                    session.StartOfflineHotseat();
                else
                    _status.text = "Start a new online match from the menu.";
            });
            UiFactory.Anchor(resetBtn.GetComponent<RectTransform>(),
                new Vector2(0.20f, 0.03f), new Vector2(0.80f, 0.10f), Vector2.zero, Vector2.zero);

            if (GameSession.Instance != null)
            {
                GameSession.Instance.OnStateChanged += Refresh;
                GameSession.Instance.OnStatusMessage += msg =>
                {
                    if (_status != null) _status.text = msg;
                };
            }
        }

        public void Show()
        {
            if (_root != null) _root.SetActive(true);
            Refresh();
            // Rebuild board cell sizes after layout activates
            Canvas.ForceUpdateCanvases();
            _boardView?.Refresh();
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void Refresh()
        {
            var session = GameSession.Instance;
            if (session == null || _turn == null) return;

            if (session.Chess.Result != GameResult.None)
            {
                _turn.text = GameSession.DescribeResult(session.Chess.Result, session.Chess.DrawReason);
                _turn.color = GameTheme.Accent;
            }
            else if (session.Chess.IsInCheck)
            {
                _turn.text = $"{session.Chess.Board.SideToMove} in check";
                _turn.color = GameTheme.Danger;
            }
            else
            {
                _turn.text = $"{session.Chess.Board.SideToMove} to move";
                _turn.color = GameTheme.TextPrimary;
            }

            _boardView?.Refresh();
        }
    }
}

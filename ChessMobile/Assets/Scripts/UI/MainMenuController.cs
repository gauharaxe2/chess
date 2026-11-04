using ChessMobile.Firebase;
using ChessMobile.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ChessMobile.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private GameObject _root;
        private InputField _joinCodeInput;
        private Text _subtitle;

        public void Build(Transform canvas)
        {
            _root = new GameObject("MainMenu");
            _root.transform.SetParent(canvas, false);
            var rootRt = _root.AddComponent<RectTransform>();
            UiFactory.Stretch(rootRt);

            UiFactory.CreatePanel(_root.transform, "Backdrop", GameTheme.BgDeep);

            // Soft gradient-like second layer
            var wash = UiFactory.CreatePanel(_root.transform, "Wash", new Color(0.15f, 0.28f, 0.20f, 0.45f));
            UiFactory.Anchor(wash.rectTransform, new Vector2(0, 0.45f), Vector2.one, Vector2.zero, Vector2.zero);

            var brand = UiFactory.CreateText(_root.transform, "Brand", "CHESS DUEL", 72, GameTheme.Accent);
            UiFactory.Anchor(brand.rectTransform, new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.90f), Vector2.zero, Vector2.zero);
            brand.fontStyle = FontStyle.Bold;

            _subtitle = UiFactory.CreateText(_root.transform, "Subtitle",
                "2D chess for two — first to 2 checkmates wins the match.",
                28, GameTheme.TextMuted);
            UiFactory.Anchor(_subtitle.rectTransform, new Vector2(0.1f, 0.66f), new Vector2(0.9f, 0.74f), Vector2.zero, Vector2.zero);

            float y = 0.58f;
            CreateMenuButton(_root.transform, "PlayGamesBtn", "Sign in with Google Play Games", new Color(0.25f, 0.42f, 0.28f), () =>
            {
                var gpg = GooglePlayGamesAuth.Instance;
                if (gpg == null)
                {
                    _subtitle.text = "Play Games auth is not ready.";
                    _subtitle.color = GameTheme.Danger;
                    return;
                }

                _subtitle.text = "Signing in with Google Play Games…";
                _subtitle.color = GameTheme.TextMuted;
                gpg.OnSignedIn -= HandlePlayGamesSignedIn;
                gpg.OnSignInFailed -= HandlePlayGamesFailed;
                gpg.OnSignedIn += HandlePlayGamesSignedIn;
                gpg.OnSignInFailed += HandlePlayGamesFailed;
                gpg.SignInAsync();
            }, ref y);

            CreateMenuButton(_root.transform, "OfflineBtn", "Offline Dual Player", GameTheme.AccentDark, () =>
            {
                GameSession.Instance.StartOfflineHotseat();
                Hide();
                FindFirstObjectByType<GameHudController>()?.Show();
            }, ref y);

            CreateMenuButton(_root.transform, "HostBtn", "Online — Host Match", GameTheme.AccentDark, () =>
            {
                GameSession.Instance.StartOnlineHost();
                Hide();
                FindFirstObjectByType<GameHudController>()?.Show();
            }, ref y);

            _joinCodeInput = UiFactory.CreateInput(_root.transform, "JoinCode", "Enter room code");
            UiFactory.Anchor(_joinCodeInput.GetComponent<RectTransform>(),
                new Vector2(0.15f, y - 0.07f), new Vector2(0.85f, y), Vector2.zero, Vector2.zero);
            y -= 0.09f;

            CreateMenuButton(_root.transform, "JoinBtn", "Online — Join Match", GameTheme.AccentDark, () =>
            {
                string code = _joinCodeInput.text?.Trim();
                if (string.IsNullOrEmpty(code))
                {
                    _subtitle.text = "Enter a room code to join.";
                    _subtitle.color = GameTheme.Danger;
                    return;
                }

                GameSession.Instance.StartOnlineJoin(code);
                Hide();
                FindFirstObjectByType<GameHudController>()?.Show();
            }, ref y);

            var footer = UiFactory.CreateText(_root.transform, "Footer",
                "Android · Google Play Games · Firebase profiles · first to 2 checkmates",
                22, GameTheme.TextMuted);
            UiFactory.Anchor(footer.rectTransform, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.10f), Vector2.zero, Vector2.zero);
        }

        private void HandlePlayGamesSignedIn(PlayerProfile profile)
        {
            if (_subtitle == null) return;
            _subtitle.text = profile != null
                ? $"Signed in as {profile.displayName} (Play Games)"
                : "Signed in with Google Play Games";
            _subtitle.color = GameTheme.Accent;
        }

        private void HandlePlayGamesFailed(string message)
        {
            if (_subtitle == null) return;
            _subtitle.text = message;
            _subtitle.color = GameTheme.Danger;
        }

        private static void CreateMenuButton(Transform parent, string name, string label, Color color, UnityEngine.Events.UnityAction action, ref float y)
        {
            var btn = UiFactory.CreateButton(parent, name, label, color, action);
            UiFactory.Anchor(btn.GetComponent<RectTransform>(),
                new Vector2(0.15f, y - 0.075f), new Vector2(0.85f, y), Vector2.zero, Vector2.zero);
            y -= 0.09f;
        }

        public void Show()
        {
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}

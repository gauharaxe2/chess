using ChessMobile.Firebase;
using ChessMobile.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChessMobile.Game
{
    /// <summary>
    /// Entry point: creates session, EventSystem, canvas, menu and HUD at runtime.
    /// Attach this to an empty GameObject in the Bootstrap scene (or use RuntimeInitialize).
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (FindFirstObjectByType<GameBootstrap>() != null)
                return;

            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(es);
            }

            if (GameSession.Instance == null)
            {
                var sessionGo = new GameObject("GameSession");
                sessionGo.AddComponent<GameSession>();
            }

            if (FirebaseProfileService.Instance == null)
            {
                var fbGo = new GameObject("FirebaseProfileService");
                fbGo.AddComponent<FirebaseProfileService>();
            }

            var canvas = UiFactory.CreateCanvas("AppCanvas");
            DontDestroyOnLoad(canvas.gameObject);

            var menu = canvas.gameObject.AddComponent<MainMenuController>();
            menu.Build(canvas.transform);

            var hud = canvas.gameObject.AddComponent<GameHudController>();
            hud.Build(canvas.transform);

            menu.Show();
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = false;
        }
    }
}

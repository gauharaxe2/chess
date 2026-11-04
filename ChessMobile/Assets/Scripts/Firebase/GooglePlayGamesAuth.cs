using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ChessMobile.Firebase
{
    /// <summary>
    /// Signs the player in with Google Play Games and exchanges the server auth code
    /// for a Firebase session (provider: playgames.google.com).
    ///
    /// Android builds should import the official Play Games Plugin for Unity and call
    /// <see cref="CompleteWithServerAuthCodeAsync"/> after Play Games sign-in succeeds.
    /// Until the plugin is present, <see cref="SignInAsync"/> falls back to a clear error
    /// on device, and Editor can use anonymous/email flows instead.
    /// </summary>
    public sealed class GooglePlayGamesAuth : MonoBehaviour
    {
        public static GooglePlayGamesAuth Instance { get; private set; }

        public bool IsSignedIn => !string.IsNullOrEmpty(IdToken);
        public string IdToken { get; private set; }
        public string LocalUid { get; private set; }
        public string DisplayName { get; private set; }

        public event Action<PlayerProfile> OnSignedIn;
        public event Action<string> OnSignInFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Entry point used by UI. On Android with Play Games plugin installed, triggers
        /// native Play Games sign-in; otherwise reports setup instructions.
        /// </summary>
        public async void SignInAsync()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                string serverAuthCode = await RequestPlayGamesServerAuthCodeAsync();
                if (string.IsNullOrEmpty(serverAuthCode))
                {
                    OnSignInFailed?.Invoke(
                        "Play Games sign-in did not return a server auth code. " +
                        "Import Play Games Plugin for Unity and link Play Games Services in Play Console.");
                    return;
                }

                bool ok = await CompleteWithServerAuthCodeAsync(serverAuthCode);
                if (!ok)
                    OnSignInFailed?.Invoke("Firebase rejected the Play Games credential.");
#else
                OnSignInFailed?.Invoke(
                    "Google Play Games sign-in runs on Android devices. " +
                    "Use the web build's Google Sign-In, or build an Android APK.");
                await Task.CompletedTask;
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlayGames] " + ex);
                OnSignInFailed?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Exchanges a Play Games server auth code for Firebase ID tokens and ensures a profile exists.
        /// </summary>
        public async Task<bool> CompleteWithServerAuthCodeAsync(string serverAuthCode)
        {
            string apiKey = FirebaseAuthConstants.AndroidApiKey;
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={apiKey}";
            string postBody = $"auth_code={UnityWebRequest.EscapeURL(serverAuthCode)}&providerId=playgames.google.com";
            string payload = JsonUtility.ToJson(new SignInWithIdpRequest
            {
                postBody = postBody,
                requestUri = "http://localhost",
                returnIdpCredential = true,
                returnSecureToken = true
            });

            using (var req = new UnityWebRequest(url, "POST"))
            {
                byte[] raw = Encoding.UTF8.GetBytes(payload);
                req.uploadHandler = new UploadHandlerRaw(raw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[PlayGames] IdP exchange failed: " + req.downloadHandler.text);
                    return false;
                }

                var auth = JsonUtility.FromJson<IdpAuthResponse>(req.downloadHandler.text);
                IdToken = auth.idToken;
                LocalUid = auth.localId;
                DisplayName = string.IsNullOrEmpty(auth.displayName) ? "Play Games Player" : auth.displayName;
            }

            var profileService = FirebaseProfileService.Instance;
            if (profileService != null)
            {
                // Reuse profile service token state
                await profileService.ApplyExternalSessionAsync(IdToken, LocalUid, DisplayName, "playgames");
                OnSignedIn?.Invoke(profileService.Current);
            }

            return true;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private Task<string> RequestPlayGamesServerAuthCodeAsync()
        {
            // Reflection keeps the project compiling without the Play Games plugin package.
            // When com.google.play.games (or the classic plugin) is imported, this resolves at runtime.
            var tcs = new TaskCompletionSource<string>();
            try
            {
                var playGamesClass = Type.GetType("GooglePlayGames.PlayGamesPlatform, Assembly-CSharp-firstpass")
                    ?? Type.GetType("GooglePlayGames.PlayGamesPlatform, Google.Play.Games");
                if (playGamesClass == null)
                {
                    tcs.SetResult(null);
                    return tcs.Task;
                }

                var instanceProp = playGamesClass.GetProperty("Instance");
                var platform = instanceProp?.GetValue(null);
                var authenticate = playGamesClass.GetMethod("Authenticate", new[] { typeof(Action<bool>) });
                if (platform == null || authenticate == null)
                {
                    tcs.SetResult(null);
                    return tcs.Task;
                }

                Action<bool> callback = success =>
                {
                    if (!success)
                    {
                        tcs.TrySetResult(null);
                        return;
                    }

                    var getCode = playGamesClass.GetMethod("RequestServerSideAccess");
                    if (getCode == null)
                    {
                        tcs.TrySetResult(null);
                        return;
                    }

                    // RequestServerSideAccess(bool forceRefresh, Action<string> callback)
                    getCode.Invoke(platform, new object[]
                    {
                        false,
                        (Action<string>)(code => tcs.TrySetResult(code))
                    });
                };

                authenticate.Invoke(platform, new object[] { callback });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PlayGames] Plugin reflection failed: " + ex.Message);
                tcs.TrySetResult(null);
            }

            return tcs.Task;
        }
#endif

        [Serializable]
        private class SignInWithIdpRequest
        {
            public string postBody;
            public string requestUri;
            public bool returnIdpCredential;
            public bool returnSecureToken;
        }

        [Serializable]
        private class IdpAuthResponse
        {
            public string idToken;
            public string localId;
            public string displayName;
            public string email;
            public string refreshToken;
        }
    }
}

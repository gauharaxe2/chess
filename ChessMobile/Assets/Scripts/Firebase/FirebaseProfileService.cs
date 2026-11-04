using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ChessMobile.Firebase
{
    /// <summary>
    /// Lightweight Firestore REST helper for profiles.
    /// Prefer the official Firebase Unity SDK in production builds; this keeps
    /// the project usable before the .unitypackage is imported.
    /// Place google-services.json under StreamingAssets (already done).
    /// </summary>
    public sealed class FirebaseProfileService : MonoBehaviour
    {
        public static FirebaseProfileService Instance { get; private set; }

        public PlayerProfile Current { get; private set; }
        public string IdToken { get; private set; }
        public string LocalUid { get; private set; }

        public event Action<PlayerProfile> OnProfileChanged;

        [SerializeField] private string projectId = "chess-duel-426622";
        [SerializeField] private string apiKey = "AIzaSyCP0AA6GGrsESYlQeh461-A0oTAkpGzLr0";

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

        public async Task<bool> SignInAnonymouslyAndEnsureProfileAsync(string displayName = null)
        {
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={apiKey}";
            string body = "{\"returnSecureToken\":true}";
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[Firebase] Anonymous sign-in failed: " + req.error + " " + req.downloadHandler.text);
                    return false;
                }

                var auth = JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
                IdToken = auth.idToken;
                LocalUid = auth.localId;
            }

            string name = string.IsNullOrWhiteSpace(displayName)
                ? "Guest" + UnityEngine.Random.Range(1000, 9999)
                : displayName.Trim();
            string username = ("guest_" + LocalUid.Substring(0, Math.Min(8, LocalUid.Length))).ToLowerInvariant();

            Current = await UpsertProfileAsync(name, username, "");
            OnProfileChanged?.Invoke(Current);
            return Current != null;
        }

        /// <summary>
        /// Applies a session from Google / Play Games IdP exchange and ensures a Firestore profile.
        /// </summary>
        public async Task ApplyExternalSessionAsync(string idToken, string uid, string displayName, string provider)
        {
            IdToken = idToken;
            LocalUid = uid;
            string trimmed = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
            string name = trimmed.Length <= 24 ? trimmed : trimmed.Substring(0, 24);
            string prefix = provider == "playgames" ? "gpg_" : "g_";
            string username = (prefix + uid.Substring(0, Math.Min(8, uid.Length))).ToLowerInvariant();
            Current = await UpsertProfileAsync(name, username, "");
            if (Current != null)
                Current.provider = provider;
            OnProfileChanged?.Invoke(Current);
        }

        public async Task<PlayerProfile> UpsertProfileAsync(string displayName, string username, string email)
        {
            if (string.IsNullOrEmpty(IdToken) || string.IsNullOrEmpty(LocalUid))
                return null;

            var profile = new PlayerProfile
            {
                uid = LocalUid,
                displayName = displayName,
                username = username,
                email = email ?? "",
                photoURL = "",
                stats = new ProfileStats()
            };

            // Firestore REST document fields use typed maps — keep payload minimal via runQuery-less PATCH.
            string docUrl =
                $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents/profiles/{LocalUid}";
            string json = BuildProfileDocument(profile);
            using (var req = new UnityWebRequest(docUrl, "PATCH"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + IdToken);
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[Firebase] Profile upsert failed: " + req.downloadHandler.text);
                    return null;
                }
            }

            return profile;
        }

        private static string BuildProfileDocument(PlayerProfile p)
        {
            // Firestore REST document format
            return "{"
                   + "\"fields\":{"
                   + $"\"uid\":{{\"stringValue\":\"{Esc(p.uid)}\"}},"
                   + $"\"displayName\":{{\"stringValue\":\"{Esc(p.displayName)}\"}},"
                   + $"\"username\":{{\"stringValue\":\"{Esc(p.username)}\"}},"
                   + $"\"email\":{{\"stringValue\":\"{Esc(p.email)}\"}},"
                   + $"\"photoURL\":{{\"stringValue\":\"{Esc(p.photoURL)}\"}},"
                   + "\"createdAt\":{\"timestampValue\":\"" + DateTime.UtcNow.ToString("o") + "\"},"
                   + "\"updatedAt\":{\"timestampValue\":\"" + DateTime.UtcNow.ToString("o") + "\"},"
                   + "\"stats\":{\"mapValue\":{\"fields\":{"
                   + "\"checkmatesScored\":{\"integerValue\":\"0\"},"
                   + "\"matchesWon\":{\"integerValue\":\"0\"},"
                   + "\"matchesLost\":{\"integerValue\":\"0\"},"
                   + "\"gamesPlayed\":{\"integerValue\":\"0\"},"
                   + "\"draws\":{\"integerValue\":\"0\"}"
                   + "}}}"
                   + "}}";
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");

        [Serializable]
        private class AuthResponse
        {
            public string idToken;
            public string localId;
            public string refreshToken;
        }
    }
}

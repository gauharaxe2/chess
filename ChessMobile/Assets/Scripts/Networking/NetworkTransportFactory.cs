using UnityEngine;

namespace ChessMobile.Networking
{
    public static class NetworkTransportFactory
    {
        /// <summary>
        /// Uses Unity Gaming Services Relay+Lobby when configured; otherwise LocalLanTransport
        /// for device testing, and EditorMockTransport in the editor for quick UI checks.
        /// </summary>
        public static INetworkTransport Create()
        {
            if (UnityServicesOnlineTransport.IsConfigured)
                return new UnityServicesOnlineTransport();

#if UNITY_EDITOR
            Debug.LogWarning("[Chess] UGS not configured — using EditorMockTransport (local echo / second player simulation).");
            return new EditorMockTransport();
#else
            Debug.LogWarning("[Chess] UGS not configured — using LocalLanTransport. Configure Unity Project ID for Play Store online play.");
            return new LocalLanTransport();
#endif
        }
    }
}

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ChessMobile.Networking
{
    /// <summary>
    /// Editor-only helper: host gets a fake room code; a "ghost" opponent is not automated.
    /// Join with the same code on a second play-mode instance is not supported —
    /// use two builds or configure UGS for real online tests.
    /// For single-editor testing, Host waits and Join connects to an in-memory bus.
    /// </summary>
    public sealed class EditorMockTransport : INetworkTransport
    {
        public event Action<string> OnRemoteMoveReceived;
        public event Action OnOpponentJoined;
        public event Action<string> OnDisconnected;

        private static EditorMockTransport _host;
        private static EditorMockTransport _client;
        private static string _activeCode;
        private bool _isHost;

        public async Task<string> HostAsync()
        {
            _isHost = true;
            _host = this;
            _activeCode = UnityEngine.Random.Range(1000, 9999).ToString();
            await Task.Yield();
            return _activeCode;
        }

        public async Task<bool> JoinAsync(string roomCode)
        {
            _isHost = false;
            await Task.Yield();
            if (_host == null || _activeCode != roomCode)
                return false;

            _client = this;
            _host.OnOpponentJoined?.Invoke();
            OnOpponentJoined?.Invoke();
            return true;
        }

        public void SendMove(string uciMove)
        {
            var other = _isHost ? _client : _host;
            if (other == null)
            {
                Debug.LogWarning("[Chess Mock] No peer connected.");
                return;
            }

            other.OnRemoteMoveReceived?.Invoke(uciMove);
        }

        public void Dispose()
        {
            if (_isHost && _host == this)
            {
                _host = null;
                _client?.OnDisconnected?.Invoke("Host left");
                _activeCode = null;
            }
            else if (!_isHost && _client == this)
            {
                _client = null;
                _host?.OnDisconnected?.Invoke("Opponent left");
            }
        }
    }
}

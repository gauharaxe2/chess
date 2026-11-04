using System;
using System.Threading.Tasks;

namespace ChessMobile.Networking
{
    /// <summary>
    /// Abstraction over online transport so we can swap Relay/Lobby, Photon, or a mock.
    /// Chess is turn-based: only UCI move strings are synced.
    /// </summary>
    public interface INetworkTransport : IDisposable
    {
        event Action<string> OnRemoteMoveReceived;
        event Action OnOpponentJoined;
        event Action<string> OnDisconnected;

        Task<string> HostAsync();
        Task<bool> JoinAsync(string roomCode);
        void SendMove(string uciMove);
    }
}

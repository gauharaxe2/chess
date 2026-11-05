using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace ChessMobile.Networking
{
    /// <summary>
    /// Play Store online path using Unity Gaming Services Authentication + Lobby.
    /// Chess is turn-based, so moves sync through lobby data (no realtime Relay required).
    /// Link a Unity Project ID under Edit → Project Settings → Services.
    /// </summary>
    public sealed class UnityServicesOnlineTransport : INetworkTransport
    {
        public event Action<string> OnRemoteMoveReceived;
        public event Action OnOpponentJoined;
        public event Action<string> OnDisconnected;

        private const string KeyLastMove = "lastMove";
        private const string KeyMoveSeq = "moveSeq";
        private const string KeyHostReady = "hostReady";

        public static bool IsConfigured
        {
            get
            {
                try
                {
                    return !string.IsNullOrEmpty(Application.cloudProjectId) &&
                           Application.cloudProjectId != "00000000-0000-0000-0000-000000000000";
                }
                catch
                {
                    return false;
                }
            }
        }

        private Lobby _lobby;
        private bool _isHost;
        private int _seenSeq;
        private CancellationTokenSource _cts;
        private bool _opponentAnnounced;

        public async Task<string> HostAsync()
        {
            _isHost = true;
            _seenSeq = 0;
            await EnsureServicesAsync();

            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { KeyHostReady, new DataObject(DataObject.VisibilityOptions.Public, "1") },
                    { KeyMoveSeq, new DataObject(DataObject.VisibilityOptions.Public, "0") },
                    { KeyLastMove, new DataObject(DataObject.VisibilityOptions.Public, "") }
                }
            };

            _lobby = await LobbyService.Instance.CreateLobbyAsync("ChessBoard", 2, options);
            _cts = new CancellationTokenSource();
            _ = PollLoopAsync(_cts.Token);
            return _lobby.LobbyCode;
        }

        public async Task<bool> JoinAsync(string roomCode)
        {
            _isHost = false;
            _seenSeq = 0;
            await EnsureServicesAsync();

            try
            {
                _lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(roomCode.Trim().ToUpperInvariant());
                OnOpponentJoined?.Invoke();
                _opponentAnnounced = true;
                _cts = new CancellationTokenSource();
                _ = PollLoopAsync(_cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[UGS Lobby] Join failed: " + ex.Message);
                OnDisconnected?.Invoke("Join failed: " + ex.Message);
                return false;
            }
        }

        public async void SendMove(string uciMove)
        {
            if (_lobby == null) return;

            try
            {
                _lobby = await LobbyService.Instance.GetLobbyAsync(_lobby.Id);
                int seq = 0;
                if (_lobby.Data != null && _lobby.Data.TryGetValue(KeyMoveSeq, out var seqData))
                    int.TryParse(seqData.Value, out seq);
                seq++;

                var update = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { KeyMoveSeq, new DataObject(DataObject.VisibilityOptions.Public, seq.ToString()) },
                        { KeyLastMove, new DataObject(DataObject.VisibilityOptions.Public, uciMove) }
                    }
                };

                _lobby = await LobbyService.Instance.UpdateLobbyAsync(_lobby.Id, update);
                _seenSeq = seq;
            }
            catch (Exception ex)
            {
                OnDisconnected?.Invoke("Failed to send move: " + ex.Message);
            }
        }

        private async Task PollLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(900, token);
                    if (_lobby == null) continue;

                    _lobby = await LobbyService.Instance.GetLobbyAsync(_lobby.Id);

                    if (!_opponentAnnounced && _lobby.Players != null && _lobby.Players.Count >= 2)
                    {
                        _opponentAnnounced = true;
                        OnOpponentJoined?.Invoke();
                    }

                    if (_lobby.Data != null &&
                        _lobby.Data.TryGetValue(KeyMoveSeq, out var seqData) &&
                        _lobby.Data.TryGetValue(KeyLastMove, out var moveData) &&
                        int.TryParse(seqData.Value, out int seq) &&
                        seq > _seenSeq &&
                        !string.IsNullOrEmpty(moveData.Value))
                    {
                        _seenSeq = seq;
                        OnRemoteMoveReceived?.Invoke(moveData.Value);
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (LobbyServiceException ex)
                {
                    OnDisconnected?.Invoke("Lobby error: " + ex.Message);
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[UGS Lobby] Poll: " + ex.Message);
                }
            }
        }

        private static async Task EnsureServicesAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { /* ignore */ }

            if (_lobby != null && _isHost)
            {
                try { LobbyService.Instance.DeleteLobbyAsync(_lobby.Id); }
                catch { /* ignore */ }
            }

            _lobby = null;
        }
    }
}

using System;
using ChessMobile.Core;
using ChessMobile.Networking;
using UnityEngine;

namespace ChessMobile.Game
{
    /// <summary>
    /// Owns the chess rules instance and routes moves for offline / online play.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        public ChessGame Chess { get; private set; }
        public GameMode Mode { get; private set; } = GameMode.OfflineHotseat;
        public PieceColor LocalPlayerColor { get; private set; } = PieceColor.White;
        public bool IsOnline => Mode != GameMode.OfflineHotseat;
        public bool CanInteract
        {
            get
            {
                if (Chess == null || Chess.Result != GameResult.None)
                    return false;
                if (!IsOnline)
                    return true;
                return Chess.Board.SideToMove == LocalPlayerColor;
            }
        }

        public event Action OnStateChanged;
        public event Action<string> OnStatusMessage;

        private INetworkTransport _network;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Chess = new ChessGame();
            Chess.OnBoardChanged += () => OnStateChanged?.Invoke();
            Chess.OnGameEnded += (result, reason) =>
            {
                OnStatusMessage?.Invoke(DescribeResult(result, reason));
                OnStateChanged?.Invoke();
            };
        }

        public void StartOfflineHotseat()
        {
            Mode = GameMode.OfflineHotseat;
            LocalPlayerColor = PieceColor.White;
            DetachNetwork();
            Chess.Reset();
            OnStatusMessage?.Invoke("Offline dual player — White to move");
            OnStateChanged?.Invoke();
        }

        public async void StartOnlineHost()
        {
            Mode = GameMode.OnlineHost;
            LocalPlayerColor = PieceColor.White;
            Chess.Reset();

            _network = NetworkTransportFactory.Create();
            _network.OnRemoteMoveReceived += HandleRemoteMove;
            _network.OnOpponentJoined += () => OnStatusMessage?.Invoke("Opponent joined. You are White.");
            _network.OnDisconnected += msg => OnStatusMessage?.Invoke(msg);

            OnStatusMessage?.Invoke("Creating online room…");
            string code = await _network.HostAsync();
            OnStatusMessage?.Invoke($"Room code: {code} — waiting for opponent");
            OnStateChanged?.Invoke();
        }

        public async void StartOnlineJoin(string roomCode)
        {
            Mode = GameMode.OnlineJoin;
            LocalPlayerColor = PieceColor.Black;
            Chess.Reset();

            _network = NetworkTransportFactory.Create();
            _network.OnRemoteMoveReceived += HandleRemoteMove;
            _network.OnOpponentJoined += () => OnStatusMessage?.Invoke("Connected. You are Black.");
            _network.OnDisconnected += msg => OnStatusMessage?.Invoke(msg);

            OnStatusMessage?.Invoke("Joining room…");
            bool ok = await _network.JoinAsync(roomCode);
            OnStatusMessage?.Invoke(ok ? "Joined — waiting for White" : "Failed to join room");
            OnStateChanged?.Invoke();
        }

        public bool TryPlayMove(Move move)
        {
            if (!CanInteract)
                return false;

            if (!Chess.TryMakeMove(move))
                return false;

            if (IsOnline && _network != null)
                _network.SendMove(move.ToUci());

            UpdateTurnStatus();
            return true;
        }

        public bool TryPlayMoveUci(string uci)
        {
            if (!CanInteract)
                return false;

            if (!Chess.TryMakeMoveUci(uci))
                return false;

            if (IsOnline && _network != null)
                _network.SendMove(uci);

            UpdateTurnStatus();
            return true;
        }

        private void HandleRemoteMove(string uci)
        {
            // Remote moves bypass CanInteract — they are the opponent's turn.
            if (!Chess.TryMakeMoveUci(uci))
            {
                OnStatusMessage?.Invoke("Received illegal remote move: " + uci);
                return;
            }

            UpdateTurnStatus();
            OnStateChanged?.Invoke();
        }

        private void UpdateTurnStatus()
        {
            if (Chess.Result != GameResult.None)
                return;

            if (!IsOnline)
            {
                OnStatusMessage?.Invoke($"{Chess.Board.SideToMove} to move");
                return;
            }

            bool myTurn = Chess.Board.SideToMove == LocalPlayerColor;
            OnStatusMessage?.Invoke(myTurn ? "Your turn" : "Opponent's turn");
        }

        private void DetachNetwork()
        {
            if (_network == null) return;
            _network.OnRemoteMoveReceived -= HandleRemoteMove;
            _network.Dispose();
            _network = null;
        }

        private void OnDestroy()
        {
            DetachNetwork();
            if (Instance == this)
                Instance = null;
        }

        public static string DescribeResult(GameResult result, DrawReason reason)
        {
            return result switch
            {
                GameResult.WhiteWins => "Checkmate — White wins",
                GameResult.BlackWins => "Checkmate — Black wins",
                GameResult.Draw => reason switch
                {
                    DrawReason.Stalemate => "Draw — Stalemate",
                    DrawReason.FiftyMoveRule => "Draw — 50-move rule",
                    DrawReason.ThreefoldRepetition => "Draw — Threefold repetition",
                    DrawReason.InsufficientMaterial => "Draw — Insufficient material",
                    _ => "Draw"
                },
                _ => "Game over"
            };
        }
    }
}

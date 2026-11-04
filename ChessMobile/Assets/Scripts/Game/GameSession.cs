using System;
using ChessMobile.Core;
using ChessMobile.Networking;
using UnityEngine;

namespace ChessMobile.Game
{
    /// <summary>
    /// Owns the chess rules instance and routes moves for offline / online play.
    /// Match win condition: first player to deliver 2 checkmates.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        public ChessGame Chess { get; private set; }
        public MatchScore Match { get; } = new MatchScore();
        public GameMode Mode { get; private set; } = GameMode.OfflineHotseat;
        public PieceColor LocalPlayerColor { get; private set; } = PieceColor.White;
        public bool IsOnline => Mode != GameMode.OfflineHotseat;
        public bool CanInteract
        {
            get
            {
                if (Chess == null || Chess.Result != GameResult.None || Match.IsMatchOver)
                    return false;
                if (!IsOnline)
                    return true;
                return Chess.Board.SideToMove == LocalPlayerColor;
            }
        }

        public event Action OnStateChanged;
        public event Action<string> OnStatusMessage;

        private INetworkTransport _network;
        private bool _scoredCurrentGame;

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
            Chess.OnGameEnded += HandleGameEnded;
        }

        public void StartOfflineHotseat()
        {
            Mode = GameMode.OfflineHotseat;
            LocalPlayerColor = PieceColor.White;
            DetachNetwork();
            BeginNewMatch("Offline dual player — first to 2 checkmates. White to move");
        }

        public async void StartOnlineHost()
        {
            Mode = GameMode.OnlineHost;
            LocalPlayerColor = PieceColor.White;

            _network = NetworkTransportFactory.Create();
            _network.OnRemoteMoveReceived += HandleRemoteMove;
            _network.OnOpponentJoined += () => OnStatusMessage?.Invoke("Opponent joined. You are White.");
            _network.OnDisconnected += msg => OnStatusMessage?.Invoke(msg);

            OnStatusMessage?.Invoke("Creating online room…");
            string code = await _network.HostAsync();
            BeginNewMatch($"Room {code} — first to 2 checkmates. Waiting for opponent");
        }

        public async void StartOnlineJoin(string roomCode)
        {
            Mode = GameMode.OnlineJoin;
            LocalPlayerColor = PieceColor.Black;

            _network = NetworkTransportFactory.Create();
            _network.OnRemoteMoveReceived += HandleRemoteMove;
            _network.OnOpponentJoined += () => OnStatusMessage?.Invoke("Connected. You are Black.");
            _network.OnDisconnected += msg => OnStatusMessage?.Invoke(msg);

            OnStatusMessage?.Invoke("Joining room…");
            bool ok = await _network.JoinAsync(roomCode);
            if (ok)
                BeginNewMatch("Joined — first to 2 checkmates. Waiting for White");
            else
                OnStatusMessage?.Invoke("Failed to join room");
            OnStateChanged?.Invoke();
        }

        /// <summary>Starts the next board after a game ends, keeping match score.</summary>
        public void StartNextGame()
        {
            if (Match.IsMatchOver)
            {
                BeginNewMatch("New match — first to 2 checkmates. White to move");
                return;
            }

            _scoredCurrentGame = false;
            Chess.Reset();
            OnStatusMessage?.Invoke($"{Match.ScoreLine()}. White to move");
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

        private void BeginNewMatch(string status)
        {
            Match.Reset();
            _scoredCurrentGame = false;
            Chess.Reset();
            OnStatusMessage?.Invoke(status);
            OnStateChanged?.Invoke();
        }

        private void HandleGameEnded(GameResult result, DrawReason reason)
        {
            if (_scoredCurrentGame)
                return;
            _scoredCurrentGame = true;

            bool matchOver = Match.ApplyGameResult(result);
            string gameLine = DescribeResult(result, reason);

            if (matchOver)
            {
                string winner = Match.MatchWinner == GameResult.WhiteWins ? "White" : "Black";
                OnStatusMessage?.Invoke(
                    $"{gameLine}. {Match.ScoreLine()}. Match over — {winner} wins!");
            }
            else if (result == GameResult.Draw)
            {
                OnStatusMessage?.Invoke($"{gameLine}. {Match.ScoreLine()}. Draws don't count — next game.");
            }
            else
            {
                OnStatusMessage?.Invoke($"{gameLine}. {Match.ScoreLine()}. First to {MatchScore.CheckmatesToWin} wins the match.");
            }

            OnStateChanged?.Invoke();
        }

        private void HandleRemoteMove(string uci)
        {
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
            if (Chess.Result != GameResult.None || Match.IsMatchOver)
                return;

            if (!IsOnline)
            {
                OnStatusMessage?.Invoke($"{Match.ScoreLine()} · {Chess.Board.SideToMove} to move");
                return;
            }

            bool myTurn = Chess.Board.SideToMove == LocalPlayerColor;
            OnStatusMessage?.Invoke($"{Match.ScoreLine()} · {(myTurn ? "Your turn" : "Opponent's turn")}");
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
                GameResult.WhiteWins => "Checkmate — White scores",
                GameResult.BlackWins => "Checkmate — Black scores",
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

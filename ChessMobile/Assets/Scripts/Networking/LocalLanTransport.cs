using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ChessMobile.Networking
{
    /// <summary>
    /// Simple TCP host/join for same-WiFi testing without Unity Gaming Services.
    /// Room code encodes the host LAN IP last two octets + port fragment for quick join.
    /// </summary>
    public sealed class LocalLanTransport : INetworkTransport
    {
        public event Action<string> OnRemoteMoveReceived;
        public event Action OnOpponentJoined;
        public event Action<string> OnDisconnected;

        private const int Port = 7777;
        private TcpListener _listener;
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private bool _isHost;

        public async Task<string> HostAsync()
        {
            _isHost = true;
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start();

            string ip = GetLocalIp();
            string code = EncodeRoomCode(ip, Port);

            _ = AcceptLoopAsync(_cts.Token);
            await Task.Yield();
            return code;
        }

        public async Task<bool> JoinAsync(string roomCode)
        {
            _isHost = false;
            _cts = new CancellationTokenSource();
            try
            {
                if (!TryDecodeRoomCode(roomCode, out string host, out int port))
                    return false;

                _client = new TcpClient();
                await _client.ConnectAsync(host, port);
                _stream = _client.GetStream();
                OnOpponentJoined?.Invoke();
                _ = ReadLoopAsync(_cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[LAN] Join failed: " + ex.Message);
                return false;
            }
        }

        public void SendMove(string uciMove)
        {
            if (_stream == null) return;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(uciMove + "\n");
                _stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                OnDisconnected?.Invoke("Send failed: " + ex.Message);
            }
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            try
            {
                _client = await _listener.AcceptTcpClientAsync();
                _stream = _client.GetStream();
                OnOpponentJoined?.Invoke();
                await ReadLoopAsync(token);
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                OnDisconnected?.Invoke("Host error: " + ex.Message);
            }
        }

        private async Task ReadLoopAsync(CancellationToken token)
        {
            var reader = new StreamReader(_stream, Encoding.UTF8);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    string line = await reader.ReadLineAsync();
                    if (line == null)
                    {
                        OnDisconnected?.Invoke("Opponent disconnected");
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(line))
                        OnRemoteMoveReceived?.Invoke(line.Trim());
                }
            }
            catch (Exception ex)
            {
                OnDisconnected?.Invoke("Connection closed: " + ex.Message);
            }
        }

        private static string GetLocalIp()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var endPoint = socket.LocalEndPoint as IPEndPoint;
                    if (endPoint != null)
                        return endPoint.Address.ToString();
                }
            }
            catch { /* fall through */ }

            return "127.0.0.1";
        }

        private static string EncodeRoomCode(string ip, int port)
        {
            // Compact human-enterable code: last two octets + port offset
            var parts = ip.Split('.');
            if (parts.Length == 4 &&
                int.TryParse(parts[2], out int a) &&
                int.TryParse(parts[3], out int b))
            {
                return $"{a:D3}{b:D3}{(port - 7000):D3}";
            }

            return "000000000";
        }

        private static bool TryDecodeRoomCode(string code, out string host, out int port)
        {
            host = null;
            port = Port;
            if (string.IsNullOrWhiteSpace(code) || code.Length < 9)
                return false;

            if (!int.TryParse(code.Substring(0, 3), out int a)) return false;
            if (!int.TryParse(code.Substring(3, 3), out int b)) return false;
            if (!int.TryParse(code.Substring(6, 3), out int pOff)) return false;

            string local = GetLocalIp();
            var parts = local.Split('.');
            if (parts.Length != 4) return false;

            host = $"{parts[0]}.{parts[1]}.{a}.{b}";
            port = 7000 + pOff;
            return true;
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _stream?.Close(); } catch { }
            try { _client?.Close(); } catch { }
            try { _listener?.Stop(); } catch { }
        }
    }
}

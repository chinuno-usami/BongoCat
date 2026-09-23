using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace BongoCat.Sync
{
    /// <summary>
    /// Background UDP listener for LAN key/tap synchronization from Mac to Steam BongoCat.
    /// Runs seamlessly inside Unity Mono process with ZERO simulated Windows keystrokes.
    ///
    /// IMPORTANT: the game ships a *stripped* UnityEngine/System/mscorlib (Unity managed
    /// stripping). Do NOT use types that the strip removed, e.g. System.Net.Sockets.UdpClient
    /// or Console.WriteLine(string). Only use APIs that actually exist in the game's
    /// BongoCat_Data/Managed assemblies. This file deliberately uses raw System.Net.Sockets.Socket.
    /// </summary>
    public static class BongoSyncReceiver
    {
        private static Socket _sock;
        private static Thread _thread;
        private static int _pendingTaps = 0;
        private static volatile bool _running = false;
        public const int DEFAULT_PORT = 39824;

        public static void Init()
        {
            if (_running) return;
            _running = true;

            try
            {
                int port = DEFAULT_PORT;
                string envPort = Environment.GetEnvironmentVariable("BONGO_SYNC_PORT");
                if (!string.IsNullOrEmpty(envPort))
                {
                    int.TryParse(envPort, out port);
                }

                _sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                // int overload only: the bool overload was stripped by Unity's linker.
                _sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
                _sock.Bind(new IPEndPoint(IPAddress.Any, port));

                _thread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "BongoSyncUDPReceiver"
                };
                _thread.Start();
            }
            catch (Exception)
            {
                // Never let the listener break the game. (Console is stripped, so no logging here.)
                _running = false;
            }
        }

        private static void ListenLoop()
        {
            byte[] buf = new byte[256];

            while (_running)
            {
                try
                {
                    int n = _sock.Receive(buf, 0, buf.Length, SocketFlags.None);
                    if (n <= 0) continue;

                    string msg = Encoding.UTF8.GetString(buf, 0, n).Trim();
                    int count = 1;

                    if (msg.StartsWith("TAP:", StringComparison.OrdinalIgnoreCase))
                    {
                        int parsed;
                        if (int.TryParse(msg.Substring(4), out parsed) && parsed > 0)
                        {
                            count = Math.Min(parsed, 50); // safety cap per packet
                        }
                    }
                    else if (msg.IndexOf("\"count\":", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            int idx = msg.IndexOf("\"count\":", StringComparison.OrdinalIgnoreCase) + 8;
                            int end = idx;
                            while (end < msg.Length && (char.IsDigit(msg[end]) || msg[end] == ' ')) end++;
                            int parsed;
                            if (int.TryParse(msg.Substring(idx, end - idx).Trim(), out parsed) && parsed > 0)
                            {
                                count = Math.Min(parsed, 50);
                            }
                        }
                        catch { }
                    }

                    Interlocked.Add(ref _pendingTaps, count);
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Called from Unity Update() to drain all taps received from Mac since last frame.
        /// </summary>
        public static int PopTaps()
        {
            if (!_running)
            {
                Init();
            }
            return Interlocked.Exchange(ref _pendingTaps, 0);
        }

        public static void Shutdown()
        {
            _running = false;
            try
            {
                if (_sock != null)
                {
                    _sock.Close();
                    _sock = null;
                }
            }
            catch { }
            _thread = null;
        }
    }
}

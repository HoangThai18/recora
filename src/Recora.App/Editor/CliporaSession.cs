using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Recora.App.Logging;
using Recora.Core.Editor;

namespace Recora.App.Editor
{
    /// <summary>
    /// A running Clipora started by Recora: <c>--control &lt;port&gt;</c> on a free loopback port and <c>--empty</c>, so it
    /// opens a blank project without taking the keyboard. Requests go over that port one line at a time. Methods block.
    /// </summary>
    internal sealed class CliporaSession
    {
        private int _nextId;

        private CliporaSession(Process process, int port, string exe)
        {
            Process = process;
            Port = port;
            Exe = exe;
        }

        public Process Process { get; }

        public int Port { get; }

        public string Exe { get; }

        public bool IsAlive
        {
            get
            {
                try
                {
                    return !Process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }

        public static CliporaSession Start(string exe)
        {
            var port = FreePort();
            var start = new ProcessStartInfo(exe, $"--control {port} --empty")
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty,
            };

            // Not attached to Recora's kill-on-close job: closing Recora must never take an unsaved edit with it.
            var process = Process.Start(start) ?? throw new InvalidOperationException("clipora.exe did not start");
            process.EnableRaisingEvents = true;
            Log.Info($"clipora started: pid {process.Id}, control port {port}, {exe}");
            return new CliporaSession(process, port, exe);
        }

        /// <summary>Clipora's main window once it is up (the largest visible top-level window of its process).</summary>
        public IntPtr WaitForWindow(TimeSpan timeout)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < timeout && IsAlive)
            {
                var window = MainWindow(Process.Id);
                if (window != IntPtr.Zero)
                {
                    return window;
                }

                Thread.Sleep(200);
            }

            return IntPtr.Zero;
        }

        /// <summary>Imports files into the open project and builds a timeline from them; returns how many it took.</summary>
        public int AddToTimeline(IReadOnlyList<string> paths)
        {
            var import = Execute("file.import", CliporaControl.ImportParams(paths));
            if (!import.Ok || import.Items.Count == 0)
            {
                Log.Warn("clipora import failed: " + (import.Error ?? import.Raw));
                return 0;
            }

            var sequence = Execute("file.newSequenceFromClip", CliporaControl.ItemsParams(import.Items));
            if (!sequence.Ok)
            {
                Log.Warn("clipora sequence from clip failed: " + sequence.Error);
            }

            return import.Items.Count;
        }

        public ControlReply Execute(string command, string paramsJson) =>
            Send(CliporaControl.Execute(Interlocked.Increment(ref _nextId), command, paramsJson));

        public ControlReply Call(string method) => Send(CliporaControl.Method(Interlocked.Increment(ref _nextId), method));

        /// <summary>One request on a fresh connection; retried while Clipora is still starting its control server.</summary>
        public ControlReply Send(string line, int attempts = 40)
        {
            for (var attempt = 0; attempt < attempts && IsAlive; attempt++)
            {
                try
                {
                    using (var client = new TcpClient())
                    {
                        client.Connect(IPAddress.Loopback, Port);
                        client.ReceiveTimeout = 65000;
                        client.SendTimeout = 5000;
                        using (var stream = client.GetStream())
                        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n" })
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            writer.WriteLine(line);
                            writer.Flush();
                            var reply = CliporaControl.Parse(reader.ReadLine());
                            Log.Info($"clipora {Shorten(line)} -> {Shorten(reply.Raw)}");
                            return reply;
                        }
                    }
                }
                catch (Exception error) when (error is SocketException || error is IOException)
                {
                    Thread.Sleep(250);
                }
            }

            return new ControlReply(false, IsAlive ? "Clipora không trả lời" : "Clipora đã đóng", string.Empty);
        }

        private static string Shorten(string text) => text.Length <= 300 ? text : text.Substring(0, 300) + "…";

        private static int FreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static IntPtr MainWindow(int processId)
        {
            var best = IntPtr.Zero;
            long bestArea = 0;
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out var owner);
                if (owner == processId && IsWindowVisible(window) && GetWindow(window, GwOwner) == IntPtr.Zero && GetWindowRect(window, out var rect))
                {
                    var area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
                    if (area > bestArea)
                    {
                        best = window;
                        bestArea = area;
                    }
                }

                return true;
            }, IntPtr.Zero);
            return best;
        }

        private const uint GwOwner = 4;

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint command);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out Rect rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}

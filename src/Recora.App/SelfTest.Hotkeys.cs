using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Recora.App.Logging;
using Recora.App.Platform;
using Recora.App.UI;

namespace Recora.App
{
    internal static partial class SelfTest
    {
        private const int VkF9 = 0x78;

        /// <summary>
        /// <c>--selftest-hotkeys &lt;dir&gt;</c>: a window in front that takes the keyboard the way League of Legends
        /// does (raw input with RIDEV_NOHOTKEYS), then F9 tapped twice by SendInput. RegisterHotKey alone (control
        /// run) may miss it; with the key-state poll Recora must act exactly twice, never four times. Then the window
        /// list must show a Notepad window that was opened for it.
        /// </summary>
        public static int Hotkeys(string directory)
        {
            Directory.CreateDirectory(directory);
            Log.Initialize(directory, echoToConsole: true);
            var lines = new StringBuilder();
            var ok = true;

            using (var game = new Form { Text = "Recora hotkey game", Width = 640, Height = 360, StartPosition = FormStartPosition.CenterScreen, TopMost = true })
            {
                game.Show();
                game.Activate();
                SetForegroundWindow(game.Handle);
                var device = new RawInputDevice { UsagePage = 1, Usage = 6, Flags = RidevNoHotkeys, Target = game.Handle };
                var raw = RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf(typeof(RawInputDevice)));
                PumpFor(TimeSpan.FromMilliseconds(500));
                var front = GetForegroundWindow() == game.Handle;
                Log.Info($"hotkeys: game window in front = {front}, raw input NOHOTKEYS registered = {raw}");
                lines.AppendLine($"game.front={front}");
                lines.AppendLine($"game.rawinput={raw}");

                foreach (var poll in new[] { false, true })
                {
                    var name = poll ? "poll" : "control";
                    var presses = 0;
                    using (var hotkeys = new HotkeyManager(poll))
                    {
                        hotkeys.Pressed += _ => presses++;
                        var failed = hotkeys.Apply(new Dictionary<HotkeyAction, string> { [HotkeyAction.Record] = "F9" });
                        lines.AppendLine($"{name}.registered={failed.Count == 0}");
                        PumpFor(TimeSpan.FromMilliseconds(200));
                        for (var tap = 0; tap < 2; tap++)
                        {
                            Tap(VkF9);
                            PumpFor(TimeSpan.FromMilliseconds(700));
                        }
                    }

                    Log.Info($"hotkeys {name}: {presses} presses seen for 2 taps");
                    lines.AppendLine($"{name}.presses={presses}");
                    if (poll && presses != 2)
                    {
                        ok = false;
                    }
                }
            }

            Process? notepad = null;
            try
            {
                notepad = Process.Start("notepad.exe");
                var listed = false;
                var clock = Stopwatch.StartNew();
                while (!listed && clock.Elapsed < TimeSpan.FromSeconds(10))
                {
                    PumpFor(TimeSpan.FromMilliseconds(300));
                    listed = WindowPicking.List().Any(w => string.Equals(w.Process, "notepad", StringComparison.OrdinalIgnoreCase));
                }

                var all = WindowPicking.List();
                Log.Info("windows listed: " + string.Join(" | ", all.Select(w => w.Label)));
                lines.AppendLine($"notepad.listed={listed}");
                lines.AppendLine($"windows.count={all.Count}");
                ok &= listed;
            }
            finally
            {
                if (notepad != null && !notepad.HasExited)
                {
                    notepad.Kill();
                }
            }

            lines.AppendLine($"ok={ok}");
            File.WriteAllText(Path.Combine(directory, "hotkeys.txt"), lines.ToString());
            Log.Info(ok ? "hotkeys selftest OK" : "hotkeys selftest FAILED");
            return ok ? 0 : 1;
        }

        private static void Tap(ushort vk)
        {
            var inputs = new[]
            {
                new KeyInput { Type = 1, Key = new KeyboardInput { Vk = vk } },
                new KeyInput { Type = 1, Key = new KeyboardInput { Vk = vk, Flags = 2 } },
            };
            SendInput(1, new[] { inputs[0] }, Marshal.SizeOf(typeof(KeyInput)));
            PumpFor(TimeSpan.FromMilliseconds(80));
            SendInput(1, new[] { inputs[1] }, Marshal.SizeOf(typeof(KeyInput)));
        }

        private const uint RidevNoHotkeys = 0x200;

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDevice
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort Vk;
            public ushort Scan;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        // INPUT is a type plus a union whose largest member (MOUSEINPUT) is 32 bytes on x64; pad to that size.
        [StructLayout(LayoutKind.Explicit, Size = 40)]
        private struct KeyInput
        {
            [FieldOffset(0)] public uint Type;
            [FieldOffset(8)] public KeyboardInput Key;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, KeyInput[] inputs, int size);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}

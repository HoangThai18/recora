using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Recora.App.Logging;
using Recora.Core.Hotkeys;

namespace Recora.App.Platform
{
    internal enum HotkeyAction
    {
        Record,
        Pause,
        Screenshot,
        SaveReplay,
    }

    /// <summary>
    /// System-wide shortcuts through RegisterHotKey on a message-only window, plus a 15 ms poll of the key state for
    /// games that block RegisterHotKey while they have focus (raw input with RIDEV_NOHOTKEYS: League of Legends).
    /// A shortcut another app already owns cannot be registered; that is reported, never silently ignored (BR-04),
    /// and the poll still catches it.
    /// </summary>
    internal sealed class HotkeyManager : NativeWindow, IDisposable
    {
        private const int WmHotkey = 0x0312;
        private const int WmPolledHotkey = 0x8000 + 0x52;
        private const int PollMs = 15;
        private const uint ModNoRepeat = 0x4000;
        private static readonly IntPtr MessageOnlyParent = new IntPtr(-3);
        private readonly Dictionary<int, HotkeyAction> _registered = new Dictionary<int, HotkeyAction>();
        private readonly HotkeyPoller<HotkeyAction> _poller = new HotkeyPoller<HotkeyAction>();
        private readonly HotkeyGate<HotkeyAction> _gate = new HotkeyGate<HotkeyAction>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Thread _pollThread;
        private volatile bool _disposed;
        private IntPtr _handle;

        /// <param name="poll">False only in the self-test's control run, to show what RegisterHotKey alone misses.</param>
        public HotkeyManager(bool poll = true)
        {
            CreateHandle(new CreateParams { Parent = MessageOnlyParent });
            _handle = Handle;
            _pollThread = new Thread(Poll) { IsBackground = true, Name = "Recora hotkey poll", Priority = ThreadPriority.AboveNormal };
            if (poll)
            {
                _pollThread.Start();
            }
        }

        public event Action<HotkeyAction>? Pressed;

        /// <summary>Registers all shortcuts, replacing previous ones. Returns the actions whose key is taken or invalid.</summary>
        public IReadOnlyList<HotkeyAction> Apply(IReadOnlyDictionary<HotkeyAction, string> keys)
        {
            UnregisterAll();
            var failed = new List<HotkeyAction>();
            var polled = new List<KeyValuePair<HotkeyAction, Hotkey>>();

            foreach (var pair in keys)
            {
                if (!Hotkey.TryParse(pair.Value, out var hotkey))
                {
                    failed.Add(pair.Key);
                    continue;
                }

                polled.Add(new KeyValuePair<HotkeyAction, Hotkey>(pair.Key, hotkey));
                var id = (int)pair.Key + 1;
                if (RegisterHotKey(Handle, id, (uint)hotkey.Modifiers | ModNoRepeat, (uint)hotkey.VirtualKey))
                {
                    _registered[id] = pair.Key;
                }
                else
                {
                    Log.Warn($"hotkey {hotkey} for {pair.Key} is taken (error {Marshal.GetLastWin32Error()})");
                    failed.Add(pair.Key);
                }
            }

            lock (_poller)
            {
                _poller.Set(polled);
            }

            return failed;
        }

        public void Dispose()
        {
            _disposed = true;
            _handle = IntPtr.Zero;
            UnregisterAll();
            DestroyHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && _registered.TryGetValue(m.WParam.ToInt32(), out var action))
            {
                Fire(action);
                return;
            }

            if (m.Msg == WmPolledHotkey)
            {
                Fire((HotkeyAction)m.WParam.ToInt32());
                return;
            }

            base.WndProc(ref m);
        }

        /// <summary>WM_HOTKEY and the poll both report a press when RegisterHotKey works; it acts once.</summary>
        private void Fire(HotkeyAction action)
        {
            if (_gate.Allow(action, _clock.ElapsedMilliseconds))
            {
                Pressed?.Invoke(action);
            }
        }

        private void Poll()
        {
            while (!_disposed)
            {
                Thread.Sleep(PollMs);
                IReadOnlyList<HotkeyAction> pressed;
                lock (_poller)
                {
                    pressed = _poller.Step(vk => (GetAsyncKeyState(vk) & 0x8000) != 0);
                }

                var target = _handle;
                foreach (var action in pressed)
                {
                    if (target != IntPtr.Zero)
                    {
                        PostMessage(target, WmPolledHotkey, new IntPtr((int)action), IntPtr.Zero);
                    }
                }
            }
        }

        private void UnregisterAll()
        {
            foreach (var id in _registered.Keys)
            {
                UnregisterHotKey(Handle, id);
            }

            _registered.Clear();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vk);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}

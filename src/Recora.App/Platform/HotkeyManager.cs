using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
    }

    /// <summary>
    /// System-wide shortcuts through RegisterHotKey on a message-only window, so F12 works while a game has focus.
    /// A shortcut another app already owns cannot be registered; that is reported, never silently ignored (BR-04).
    /// </summary>
    internal sealed class HotkeyManager : NativeWindow, IDisposable
    {
        private const int WmHotkey = 0x0312;
        private const uint ModNoRepeat = 0x4000;
        private static readonly IntPtr MessageOnlyParent = new IntPtr(-3);
        private readonly Dictionary<int, HotkeyAction> _registered = new Dictionary<int, HotkeyAction>();

        public HotkeyManager()
        {
            CreateHandle(new CreateParams { Parent = MessageOnlyParent });
        }

        public event Action<HotkeyAction>? Pressed;

        /// <summary>Registers all shortcuts, replacing previous ones. Returns the actions whose key is taken or invalid.</summary>
        public IReadOnlyList<HotkeyAction> Apply(IReadOnlyDictionary<HotkeyAction, string> keys)
        {
            UnregisterAll();
            var failed = new List<HotkeyAction>();

            foreach (var pair in keys)
            {
                if (!Hotkey.TryParse(pair.Value, out var hotkey))
                {
                    failed.Add(pair.Key);
                    continue;
                }

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

            return failed;
        }

        public void Dispose()
        {
            UnregisterAll();
            DestroyHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && _registered.TryGetValue(m.WParam.ToInt32(), out var action))
            {
                Pressed?.Invoke(action);
                return;
            }

            base.WndProc(ref m);
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
    }
}

using System;
using System.Collections.Generic;

namespace Recora.Core.Hotkeys
{
    /// <summary>
    /// Reads shortcuts from the raw key state, the way OBS does. Games that register raw keyboard input with
    /// RIDEV_NOHOTKEYS (League of Legends among them) swallow RegisterHotKey while they have focus; the key state
    /// still changes, so a poll on it sees F12 inside the game.
    /// </summary>
    public sealed class HotkeyPoller<TAction>
    {
        public const int VkShift = 0x10;
        public const int VkControl = 0x11;
        public const int VkAlt = 0x12;
        public const int VkLeftWin = 0x5B;
        public const int VkRightWin = 0x5C;

        private readonly List<(TAction Action, Hotkey Key)> _keys = new List<(TAction, Hotkey)>();
        private readonly HashSet<int> _held = new HashSet<int>();

        public void Set(IEnumerable<KeyValuePair<TAction, Hotkey>> keys)
        {
            _keys.Clear();
            _held.Clear();
            foreach (var pair in keys)
            {
                _keys.Add((pair.Key, pair.Value));
            }
        }

        /// <summary>
        /// One look at the keyboard. Returns the actions whose shortcut has just gone down: its key is down, exactly
        /// its modifiers are held (Shift+F12 is not F12), and it was not already down on the previous look.
        /// </summary>
        public IReadOnlyList<TAction> Step(Func<int, bool> isDown)
        {
            var modifiers = HotkeyModifiers.None;
            if (isDown(VkShift))
            {
                modifiers |= HotkeyModifiers.Shift;
            }

            if (isDown(VkControl))
            {
                modifiers |= HotkeyModifiers.Control;
            }

            if (isDown(VkAlt))
            {
                modifiers |= HotkeyModifiers.Alt;
            }

            if (isDown(VkLeftWin) || isDown(VkRightWin))
            {
                modifiers |= HotkeyModifiers.Win;
            }

            var pressed = new List<TAction>();
            for (var i = 0; i < _keys.Count; i++)
            {
                var (action, key) = _keys[i];
                var active = isDown(key.VirtualKey) && modifiers == key.Modifiers;
                if (!active)
                {
                    _held.Remove(i);
                    continue;
                }

                if (_held.Add(i))
                {
                    pressed.Add(action);
                }
            }

            return pressed;
        }
    }

    /// <summary>
    /// One press reaches Recora twice when RegisterHotKey works too (WM_HOTKEY and the key-state poll); the second
    /// within the window is dropped, so F12 never starts and at once stops a recording.
    /// </summary>
    public sealed class HotkeyGate<TAction>
    {
        private readonly Dictionary<TAction, long> _last = new Dictionary<TAction, long>();
        private readonly long _windowMs;

        public HotkeyGate(long windowMs = 400)
        {
            _windowMs = windowMs;
        }

        public bool Allow(TAction action, long nowMs)
        {
            if (_last.TryGetValue(action, out var last) && nowMs - last < _windowMs)
            {
                return false;
            }

            _last[action] = nowMs;
            return true;
        }
    }
}

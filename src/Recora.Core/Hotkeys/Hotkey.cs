using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Recora.Core.Hotkeys
{
    /// <summary>Modifier bits, valued like RegisterHotKey's MOD_* flags so the app can pass them straight through.</summary>
    [Flags]
    public enum HotkeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8,
    }

    /// <summary>A global shortcut such as <c>Shift+F12</c>: modifiers plus a Windows virtual-key code.</summary>
    public readonly record struct Hotkey(HotkeyModifiers Modifiers, int VirtualKey)
    {
        private static readonly IReadOnlyDictionary<string, int> NamedKeys = BuildKeys();
        private static readonly IReadOnlyDictionary<int, string> KeyNames =
            NamedKeys.GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.First().Key);

        public static Hotkey Record => new Hotkey(HotkeyModifiers.None, 0x7B);

        public static Hotkey Pause => new Hotkey(HotkeyModifiers.Shift, 0x7B);

        public static Hotkey Screenshot => new Hotkey(HotkeyModifiers.None, 0x7A);

        public static bool TryParse(string? text, out Hotkey hotkey)
        {
            hotkey = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var modifiers = HotkeyModifiers.None;
            int? key = null;

            foreach (var raw in text!.Split('+'))
            {
                var part = raw.Trim();
                switch (part.ToUpperInvariant())
                {
                    case "CTRL":
                    case "CONTROL":
                        modifiers |= HotkeyModifiers.Control;
                        continue;
                    case "ALT":
                        modifiers |= HotkeyModifiers.Alt;
                        continue;
                    case "SHIFT":
                        modifiers |= HotkeyModifiers.Shift;
                        continue;
                    case "WIN":
                        modifiers |= HotkeyModifiers.Win;
                        continue;
                }

                if (key.HasValue || !NamedKeys.TryGetValue(part, out var vk))
                {
                    return false;
                }

                key = vk;
            }

            if (!key.HasValue)
            {
                return false;
            }

            hotkey = new Hotkey(modifiers, key.Value);
            return true;
        }

        public static string? NameOf(int virtualKey) => KeyNames.TryGetValue(virtualKey, out var name) ? name : null;

        public override string ToString()
        {
            var parts = new List<string>();
            if ((Modifiers & HotkeyModifiers.Control) != 0)
            {
                parts.Add("Ctrl");
            }

            if ((Modifiers & HotkeyModifiers.Alt) != 0)
            {
                parts.Add("Alt");
            }

            if ((Modifiers & HotkeyModifiers.Shift) != 0)
            {
                parts.Add("Shift");
            }

            if ((Modifiers & HotkeyModifiers.Win) != 0)
            {
                parts.Add("Win");
            }

            parts.Add(NameOf(VirtualKey) ?? "0x" + VirtualKey.ToString("X2", CultureInfo.InvariantCulture));
            return string.Join("+", parts);
        }

        private static IReadOnlyDictionary<string, int> BuildKeys()
        {
            var keys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i <= 24; i++)
            {
                keys["F" + i.ToString(CultureInfo.InvariantCulture)] = 0x6F + i;
            }

            for (var c = 'A'; c <= 'Z'; c++)
            {
                keys[c.ToString()] = c;
            }

            for (var d = 0; d <= 9; d++)
            {
                keys[d.ToString(CultureInfo.InvariantCulture)] = 0x30 + d;
                keys["Num" + d.ToString(CultureInfo.InvariantCulture)] = 0x60 + d;
            }

            keys["PrintScreen"] = 0x2C;
            keys["Pause"] = 0x13;
            keys["ScrollLock"] = 0x91;
            keys["Insert"] = 0x2D;
            keys["Delete"] = 0x2E;
            keys["Home"] = 0x24;
            keys["End"] = 0x23;
            keys["PageUp"] = 0x21;
            keys["PageDown"] = 0x22;
            keys["Space"] = 0x20;
            return keys;
        }
    }
}

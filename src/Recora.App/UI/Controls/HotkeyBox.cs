using System;
using System.Drawing;
using System.Windows.Forms;
using Recora.Core.Hotkeys;

namespace Recora.App.UI.Controls
{
    /// <summary>
    /// Click, then press the new combination. Esc cancels, Backspace keeps the old one. Modifier keys alone are not a
    /// shortcut, so the box waits until a real key comes with them.
    /// </summary>
    internal sealed class HotkeyBox : Control
    {
        private Hotkey _hotkey;
        private bool _listening;
        private bool _conflict;

        public HotkeyBox(Hotkey hotkey)
        {
            _hotkey = hotkey;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Font = Theme.Mono;
            AccessibleRole = AccessibleRole.HotkeyField;
            AccessibleName = hotkey.ToString();
            Size = new Size(190, 34);
        }

        public event EventHandler? HotkeyChanged;

        public Hotkey Hotkey
        {
            get => _hotkey;
            set
            {
                _hotkey = value;
                AccessibleName = value.ToString();
                Invalidate();
            }
        }

        /// <summary>Marks the box red when Windows refused the shortcut because another app owns it.</summary>
        public bool Conflict
        {
            get => _conflict;
            set
            {
                _conflict = value;
                Invalidate();
            }
        }

        protected override bool IsInputKey(Keys keyData) => _listening || base.IsInputKey(keyData);

        protected override void OnClick(EventArgs e)
        {
            Focus();
            _listening = true;
            Invalidate();
            base.OnClick(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            _listening = false;
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // F10 and Alt+key would otherwise be eaten by the menu loop before OnKeyDown sees them.
            if (_listening && TryAssign(keyData))
            {
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var dpi = DeviceDpi / 96f * Theme.UiScale;
            var box = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            var border = _conflict ? Theme.Record : _listening || Focused ? Theme.Accent : Theme.Border;
            Theme.FillRounded(g, _listening ? Theme.AccentHover : Theme.Background, box, 7 * dpi);
            Theme.StrokeRounded(g, border, (_listening ? 1.6f : 1f) * dpi, box, 7 * dpi);
            var text = _listening ? "Bấm tổ hợp phím…" : _hotkey.ToString();
            TextRenderer.DrawText(g, text, _listening ? Theme.Body : Theme.Mono, Rectangle.Round(box), _conflict ? Theme.Record : Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private bool TryAssign(Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            if (key == Keys.Escape || key == Keys.Back)
            {
                _listening = false;
                Invalidate();
                return true;
            }

            if (key == Keys.ShiftKey || key == Keys.ControlKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin || Hotkey.NameOf((int)key) == null)
            {
                return true;
            }

            var modifiers = HotkeyModifiers.None;
            if ((keyData & Keys.Control) != 0)
            {
                modifiers |= HotkeyModifiers.Control;
            }

            if ((keyData & Keys.Alt) != 0)
            {
                modifiers |= HotkeyModifiers.Alt;
            }

            if ((keyData & Keys.Shift) != 0)
            {
                modifiers |= HotkeyModifiers.Shift;
            }

            _hotkey = new Hotkey(modifiers, (int)key);
            _listening = false;
            _conflict = false;
            AccessibleName = _hotkey.ToString();
            Invalidate();
            HotkeyChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }
}

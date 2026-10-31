using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Recora.Core.Recording;

namespace Recora.App.UI
{
    /// <summary>
    /// The notification-area icon: Recora keeps recording while its window is hidden, and the icon turns red with a
    /// dot so the user always knows the screen is being captured.
    /// </summary>
    internal sealed class TrayIcon : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly ToolStripMenuItem _record;
        private readonly ToolStripMenuItem _pause;
        private readonly Icon _idleIcon;
        private readonly Icon _recordingIcon;
        private readonly Icon _pausedIcon;

        public TrayIcon()
        {
            _idleIcon = AppIcon.Small;
            _recordingIcon = WithDot(Theme.Record);
            _pausedIcon = WithDot(Theme.Pause);

            var menu = new ContextMenuStrip { Font = Theme.Body, ShowImageMargin = false };
            var open = new ToolStripMenuItem("Mở Recora") { Font = Theme.BodyBold };
            _record = new ToolStripMenuItem("Bắt đầu ghi");
            _pause = new ToolStripMenuItem("Tạm dừng") { Enabled = false };
            var folder = new ToolStripMenuItem("Mở thư mục video");
            var exit = new ToolStripMenuItem("Thoát");
            open.Click += (_, _) => OpenClicked?.Invoke(this, EventArgs.Empty);
            _record.Click += (_, _) => RecordClicked?.Invoke(this, EventArgs.Empty);
            _pause.Click += (_, _) => PauseClicked?.Invoke(this, EventArgs.Empty);
            folder.Click += (_, _) => FolderClicked?.Invoke(this, EventArgs.Empty);
            exit.Click += (_, _) => ExitClicked?.Invoke(this, EventArgs.Empty);
            menu.Items.AddRange(new ToolStripItem[] { open, new ToolStripSeparator(), _record, _pause, folder, new ToolStripSeparator(), exit });

            _icon = new NotifyIcon { Icon = _idleIcon, Text = "Recora", ContextMenuStrip = menu, Visible = true };
            _icon.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    OpenClicked?.Invoke(this, EventArgs.Empty);
                }
            };
            _icon.BalloonTipClicked += (_, _) => BalloonClicked?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? OpenClicked;

        public event EventHandler? RecordClicked;

        public event EventHandler? PauseClicked;

        public event EventHandler? FolderClicked;

        public event EventHandler? ExitClicked;

        public event EventHandler? BalloonClicked;

        public void Show(SessionState state, string tooltip)
        {
            var busy = state == SessionState.Recording || state == SessionState.Paused || state == SessionState.Starting;
            _icon.Icon = state == SessionState.Paused ? _pausedIcon : busy ? _recordingIcon : _idleIcon;
            _record.Text = busy ? "Dừng và lưu" : "Bắt đầu ghi";
            _record.Enabled = state != SessionState.Finalizing;
            _pause.Enabled = state == SessionState.Recording || state == SessionState.Paused;
            _pause.Text = state == SessionState.Paused ? "Ghi tiếp" : "Tạm dừng";

            // NotifyIcon.Text throws above 63 characters.
            _icon.Text = tooltip.Length > 63 ? tooltip.Substring(0, 62) + "…" : tooltip;
        }

        public void Notify(string title, string text, ToolTipIcon kind = ToolTipIcon.Info) =>
            _icon.ShowBalloonTip(4000, title, text, kind);

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.ContextMenuStrip?.Dispose();
            _icon.Dispose();
            _recordingIcon.Dispose();
            _pausedIcon.Dispose();
        }

        /// <summary>The app icon with a coloured dot in the lower-right corner, built at the tray's size.</summary>
        private static Icon WithDot(Color color)
        {
            var size = SystemInformation.SmallIconSize;
            using (var bitmap = new Bitmap(size.Width, size.Height))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(AppIcon.Bitmap, new Rectangle(Point.Empty, size));
                    var dot = size.Width * 0.5f;
                    var box = new RectangleF(size.Width - dot - 0.5f, size.Height - dot - 0.5f, dot, dot);
                    using (var ring = new SolidBrush(Color.White))
                    using (var fill = new SolidBrush(color))
                    {
                        g.FillEllipse(ring, RectangleF.Inflate(box, 1, 1));
                        g.FillEllipse(fill, box);
                    }
                }

                var handle = bitmap.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(handle).Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}

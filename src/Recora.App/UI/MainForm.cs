using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Recora.App.UI.Controls;

namespace Recora.App.UI
{
    internal enum CaptureModeChoice
    {
        FullScreen,
        Region,
        Window,
        Game,
        Device,
        AudioOnly,
    }

    /// <summary>
    /// The main window: capture modes and the record button on top, pages on the left, a blue status bar at the bottom.
    /// It only shows state and raises events; recording logic lives outside the UI.
    /// </summary>
    internal sealed class MainForm : Form
    {
        private readonly Dictionary<CaptureModeChoice, ModeButton> _modes = new Dictionary<CaptureModeChoice, ModeButton>();
        private readonly Dictionary<PageKind, NavButton> _nav = new Dictionary<PageKind, NavButton>();
        private readonly Dictionary<PageKind, Page> _pages = new Dictionary<PageKind, Page>();
        private readonly Panel _content;

        public MainForm()
        {
            Text = "Recora";
            Font = Theme.Body;
            BackColor = Theme.Background;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f / Theme.UiScale, 96f / Theme.UiScale);
            ClientSize = new Size(900, 580);
            MinimumSize = new Size(800, 540);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = AppIcon.Large;

            _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            Status = new AppStatusBar { Dock = DockStyle.Bottom, Height = 30 };

            // Docking runs from the last added control to the first: top bar and status bar span the full width.
            Controls.Add(_content);
            Controls.Add(BuildSidebar());
            Controls.Add(Status);
            Controls.Add(BuildTopBar());

            Home = new HomePage();
            AddPage(PageKind.Home, Home);
            AddPage(PageKind.Video, new PlaceholderPage("Video", "Chất lượng, số khung hình mỗi giây, bộ mã hoá, codec và định dạng file."));
            AddPage(PageKind.Audio, new PlaceholderPage("Âm thanh", "Chọn loa và micro, âm lượng từng nguồn, trộn chung hoặc tách hai track."));
            AddPage(PageKind.Screenshot, new PlaceholderPage("Ảnh chụp", "Định dạng PNG hoặc JPG, chép vào clipboard, âm thanh khi chụp."));
            AddPage(PageKind.Hotkeys, new PlaceholderPage("Phím tắt", "Đổi phím ghi, tạm dừng và chụp ảnh."));
            AddPage(PageKind.General, new PlaceholderPage("Chung", "Thư mục lưu, mở cùng Windows, ẩn xuống khay khi đóng."));
            AddPage(PageKind.Library, new PlaceholderPage("Thư viện", "Các video và ảnh đã ghi, mở, xem thư mục hoặc xoá."));
            AddPage(PageKind.About, new PlaceholderPage("Giới thiệu", "Recora — app quay màn hình miễn phí của thaipro.store, mã nguồn mở MIT."));

            SelectMode(CaptureModeChoice.FullScreen);
            ShowPage(PageKind.Home);
            Home.ChangeQuality.Click += (_, _) => ShowPage(PageKind.Video);
        }

        public event EventHandler<CaptureModeChoice>? ModeSelected;

        public event EventHandler? RecordClicked;

        public event EventHandler? PauseClicked;

        public event EventHandler? ScreenshotClicked;

        public HomePage Home { get; }

        public AppStatusBar Status { get; }

        public RecordButton Record { get; private set; } = null!;

        public IconButton Pause { get; private set; } = null!;

        public IconButton Screenshot { get; private set; } = null!;

        public CaptureModeChoice Mode { get; private set; }

        public void SelectMode(CaptureModeChoice mode)
        {
            Mode = mode;
            foreach (var pair in _modes)
            {
                pair.Value.Selected = pair.Key == mode;
            }
        }

        public void ShowPage(PageKind kind)
        {
            foreach (var pair in _pages)
            {
                pair.Value.Visible = pair.Key == kind;
                _nav[pair.Key].Selected = pair.Key == kind;
            }
        }

        private void AddPage(PageKind kind, Page page)
        {
            page.Visible = false;
            _pages[kind] = page;
            _content.Controls.Add(page);
        }

        private Control BuildTopBar()
        {
            var bar = new BorderedPanel(bottomBorder: true) { Dock = DockStyle.Top, Height = 78, BackColor = Theme.Surface, Padding = new Padding(10, 6, 14, 6) };

            var brand = new BrandMark { Dock = DockStyle.Left, Width = 150 };

            var actions = new Panel { Dock = DockStyle.Right, Width = 176, BackColor = Theme.Surface };
            Screenshot = new IconButton(IconKind.Camera, "Chụp ảnh (F11)") { Bounds = new Rectangle(0, 12, 44, 44) };
            Pause = new IconButton(IconKind.Pause, "Tạm dừng (Shift+F12)") { Bounds = new Rectangle(50, 12, 44, 44), Enabled = false, Tint = Theme.Pause };
            Record = new RecordButton { Bounds = new Rectangle(102, 0, 66, 66) };
            Screenshot.Click += (_, _) => ScreenshotClicked?.Invoke(this, EventArgs.Empty);
            Pause.Click += (_, _) => PauseClicked?.Invoke(this, EventArgs.Empty);
            Record.Click += (_, _) => RecordClicked?.Invoke(this, EventArgs.Empty);
            actions.Controls.AddRange(new Control[] { Screenshot, Pause, Record });

            var modes = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Theme.Surface, Padding = new Padding(4, 0, 0, 0) };
            AddMode(modes, CaptureModeChoice.FullScreen, IconKind.Monitor, "Toàn màn hình");
            AddMode(modes, CaptureModeChoice.Region, IconKind.Region, "Chọn vùng");
            AddMode(modes, CaptureModeChoice.Window, IconKind.Window, "Cửa sổ");
            AddMode(modes, CaptureModeChoice.Game, IconKind.Game, "Game");
            AddMode(modes, CaptureModeChoice.Device, IconKind.Webcam, "Webcam");
            AddMode(modes, CaptureModeChoice.AudioOnly, IconKind.Audio, "Âm thanh");

            bar.Controls.Add(modes);
            bar.Controls.Add(actions);
            bar.Controls.Add(brand);
            return bar;
        }

        private void AddMode(FlowLayoutPanel host, CaptureModeChoice mode, IconKind icon, string label)
        {
            var button = new ModeButton(icon, label) { Size = new Size(84, 64), Margin = new Padding(0) };
            button.Click += (_, _) =>
            {
                SelectMode(mode);
                ModeSelected?.Invoke(this, mode);
            };
            _modes[mode] = button;
            host.Controls.Add(button);
        }

        private Control BuildSidebar()
        {
            var side = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 184,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Sidebar,
                Padding = new Padding(0, 12, 0, 0),
            };

            var items = new (PageKind Kind, IconKind Icon, string Text)[]
            {
                (PageKind.Home, IconKind.Home, "Trang chủ"),
                (PageKind.Video, IconKind.Video, "Video"),
                (PageKind.Audio, IconKind.Speaker, "Âm thanh"),
                (PageKind.Screenshot, IconKind.Camera, "Ảnh chụp"),
                (PageKind.Hotkeys, IconKind.Keyboard, "Phím tắt"),
                (PageKind.General, IconKind.Sliders, "Chung"),
                (PageKind.Library, IconKind.Library, "Thư viện"),
                (PageKind.About, IconKind.Info, "Giới thiệu"),
            };

            foreach (var item in items)
            {
                var button = new NavButton(item.Icon, item.Text) { Size = new Size(184, 42), Margin = Padding.Empty };
                button.Click += (_, _) => ShowPage(item.Kind);
                _nav[item.Kind] = button;
                side.Controls.Add(button);
            }

            return side;
        }

        /// <summary>The app icon and name in the top-left corner.</summary>
        private sealed class BrandMark : Control
        {
            public BrandMark()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = Theme.Surface;
                AccessibleName = "Recora";
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                Theme.Smooth(g);
                var dpi = DeviceDpi / 96f * Theme.UiScale;
                var size = (int)(34 * dpi);
                var top = (Height - size) / 2;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(AppIcon.Bitmap, new Rectangle((int)(6 * dpi), top, size, size));
                TextRenderer.DrawText(g, "Recora", Theme.Brand, new Rectangle((int)(6 * dpi) + size + (int)(8 * dpi), 0, Width, Height), Theme.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
        }

        private sealed class BorderedPanel : Panel
        {
            private readonly bool _bottom;

            public BorderedPanel(bool bottomBorder)
            {
                _bottom = bottomBorder;
                SetStyle(ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (_bottom)
                {
                    using (var pen = new Pen(Theme.Border))
                    {
                        e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
                    }
                }
            }
        }
    }
}

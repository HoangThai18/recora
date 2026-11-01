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
    /// The main window: capture modes and the record button on top, pages on the left, a status line at the bottom.
    /// It only shows state and raises events; recording logic lives outside the UI.
    /// </summary>
    internal sealed class MainForm : Form
    {
        private readonly Dictionary<CaptureModeChoice, ModeButton> _modes = new Dictionary<CaptureModeChoice, ModeButton>();
        private readonly Dictionary<PageKind, NavButton> _nav = new Dictionary<PageKind, NavButton>();
        private readonly Dictionary<PageKind, Page> _pages = new Dictionary<PageKind, Page>();
        private readonly Panel _content;
        private readonly PageFade _fade = new PageFade();

        public MainForm()
        {
            // Sizes below are 96-DPI design units. WinForms only scales them to the monitor's DPI when the layout is
            // resumed after AutoScaleDimensions was set, as designer code does; without this pair, 150% screens get
            // bigger text in unscaled boxes.
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96f / Theme.UiScale, 96f / Theme.UiScale);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Recora";
            Font = Theme.Body;
            BackColor = Theme.Background;
            ClientSize = new Size(960, 680);
            MinimumSize = new Size(820, 560);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = AppIcon.Large;

            _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };
            Status = new AppStatusBar { Dock = DockStyle.Bottom, Height = 30 };

            // Docking runs from the last added control to the first: top bar and status bar span the full width.
            Controls.Add(_content);
            _content.Controls.Add(_fade);
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
            AddPage(PageKind.Edit, new PlaceholderPage("Dựng phim", "Dựng video ngay trong Recora bằng Clipora."));
            AddPage(PageKind.About, new PlaceholderPage("Giới thiệu", "Recora — app quay màn hình miễn phí của thaipro.store, mã nguồn mở MIT."));

            SelectMode(CaptureModeChoice.FullScreen);
            ShowPage(PageKind.Home);
            Home.ChangeQuality.Click += (_, _) => ShowPage(PageKind.Video);

            ResumeLayout(false);
            PerformLayout();
        }

        public event EventHandler<CaptureModeChoice>? ModeSelected;

        /// <summary>A second Recora was launched and asked this one to come to the front.</summary>
        public event EventHandler? ShowRequested;

        public event EventHandler? RecordClicked;

        public event EventHandler? PauseClicked;

        public event EventHandler? ScreenshotClicked;

        public HomePage Home { get; }

        public AppStatusBar Status { get; }

        /// <summary>Free space where videos are saved, at the foot of the sidebar.</summary>
        public DiskGauge Disk { get; private set; } = null!;

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

        public PageKind CurrentPage { get; private set; }

        public void ShowPage(PageKind kind)
        {
            var animate = kind != CurrentPage && Animator.Enabled && Visible && WindowState != FormWindowState.Minimized && IsHandleCreated;
            var before = animate ? PageFade.Snapshot(_content) : null;

            CurrentPage = kind;
            foreach (var pair in _pages)
            {
                pair.Value.Visible = pair.Key == kind;
                _nav[pair.Key].Selected = pair.Key == kind;
            }

            if (before != null)
            {
                _fade.BackColor = _content.BackColor;
                _fade.Play(_content, before);
            }
        }

        /// <summary>Swaps a page (the placeholders, once settings and the probe are ready) and keeps it shown if it was.</summary>
        public void SetPage(PageKind kind, Page page)
        {
            if (_pages.TryGetValue(kind, out var old))
            {
                _content.Controls.Remove(old);
                old.Dispose();
            }

            // Pages built after the form scaled itself to the screen's DPI must be scaled the same way, or their
            // 96-DPI sizes stay small while the text grows (cut buttons at 150%).
            var factor = DeviceDpi / 96f * Theme.UiScale;
            if (Math.Abs(factor - 1f) > 0.01f)
            {
                page.Scale(new SizeF(factor, factor));
            }

            page.Visible = _nav[kind].Selected;
            _pages[kind] = page;
            _content.Controls.Add(page);
            Theme.StyleScrollBars(page);
        }

        public T? PageOf<T>(PageKind kind)
            where T : Page => _pages.TryGetValue(kind, out var page) ? page as T : null;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // The title bar takes the window colour (Windows 11) and goes dark with the dark palette (Windows 10 2004+).
            Theme.StyleWindow(this);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Platform.SingleInstance.ShowMessage && m.Msg != 0)
            {
                ShowRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            base.WndProc(ref m);
        }

        private void AddPage(PageKind kind, Page page)
        {
            page.Visible = false;
            _pages[kind] = page;
            _content.Controls.Add(page);
        }

        private Control BuildTopBar()
        {
            var bar = new BorderedPanel(bottomBorder: true) { Dock = DockStyle.Top, Height = 78, BackColor = Theme.Background, Padding = new Padding(10, 6, 14, 6) };

            var brand = new BrandMark { Dock = DockStyle.Left, Width = 150 };

            var actions = new Panel { Dock = DockStyle.Right, Width = 176, BackColor = Theme.Background };
            Screenshot = new IconButton(IconKind.Camera, "Chụp ảnh (F11)") { Bounds = new Rectangle(0, 12, 44, 44) };
            Pause = new IconButton(IconKind.Pause, "Tạm dừng (Shift+F12)") { Bounds = new Rectangle(50, 12, 44, 44), Enabled = false, Tint = Theme.Pause };
            Record = new RecordButton { Bounds = new Rectangle(102, 0, 66, 66) };
            Screenshot.Click += (_, _) => ScreenshotClicked?.Invoke(this, EventArgs.Empty);
            Pause.Click += (_, _) => PauseClicked?.Invoke(this, EventArgs.Empty);
            Record.Click += (_, _) => RecordClicked?.Invoke(this, EventArgs.Empty);
            actions.Controls.AddRange(new Control[] { Screenshot, Pause, Record });

            var modes = new ModeBar { Dock = DockStyle.Fill, BackColor = Theme.Background, Padding = new Padding(4, 0, 4, 0) };
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

        private void AddMode(Control host, CaptureModeChoice mode, IconKind icon, string label)
        {
            var button = new ModeButton(icon, label) { Size = new Size(94, 64), Margin = new Padding(0) };
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
            var host = new Panel { Dock = DockStyle.Left, Width = 184, BackColor = Theme.Sidebar };
            Disk = new DiskGauge { Dock = DockStyle.Bottom, Height = 54 };
            var version = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Text = "Recora " + Program.Version + " · miễn phí",
                Font = Theme.Small,
                ForeColor = Theme.Muted,
                BackColor = Theme.Sidebar,
                Padding = new Padding(14, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            var side = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
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
                (PageKind.Edit, IconKind.Film, "Dựng phim"),
                (PageKind.About, IconKind.Info, "Giới thiệu"),
            };

            foreach (var item in items)
            {
                var button = new NavButton(item.Icon, item.Text) { Size = new Size(184, 42), Margin = Padding.Empty };
                button.Click += (_, _) => ShowPage(item.Kind);
                _nav[item.Kind] = button;
                side.Controls.Add(button);
            }

            host.Controls.Add(side);
            host.Controls.Add(version);
            host.Controls.Add(Disk);
            return host;
        }

        /// <summary>Spreads the mode tiles evenly and shrinks them (labels ellipsize) when the window is narrow.</summary>
        private sealed class ModeBar : Panel
        {
            protected override void OnLayout(LayoutEventArgs levent)
            {
                base.OnLayout(levent);
                var tiles = Controls.OfType<ModeButton>().ToList();
                if (tiles.Count == 0)
                {
                    return;
                }

                var dpi = DeviceDpi / 96f * Theme.UiScale;
                var available = ClientSize.Width - Padding.Horizontal;
                var width = Math.Max((int)(56 * dpi), Math.Min((int)(94 * dpi), available / tiles.Count));
                var height = (int)(64 * dpi);
                var top = Math.Max(0, (ClientSize.Height - height) / 2);

                for (var i = 0; i < tiles.Count; i++)
                {
                    tiles[i].Bounds = new Rectangle(Padding.Left + i * width, top, width, height);
                }
            }
        }

        /// <summary>The app icon and name in the top-left corner.</summary>
        private sealed class BrandMark : Control
        {
            public BrandMark()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                BackColor = Theme.Background;
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

        internal sealed class BorderedPanel : Panel
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

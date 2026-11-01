using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Recora.Core.Animation;

namespace Recora.App.UI.Controls
{
    /// <summary>Base for small painted widgets: double buffered, transparent-friendly, DPI-aware.</summary>
    internal abstract class Widget : Control
    {
        protected Widget()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Theme.Surface;
        }

        protected float Dpi => DeviceDpi / 96f * Theme.UiScale;

        protected int S(float value) => (int)Math.Round(value * Dpi);
    }

    /// <summary>
    /// A 16:9 picture of what will be recorded, refreshed by the app while idle. While recording it turns into a dark
    /// panel with a big clock, so the busiest moment of the app is also the easiest to read.
    /// </summary>
    internal sealed class PreviewBox : Widget
    {
        private Image? _image;
        private static readonly TimeSpan Blink = TimeSpan.FromMilliseconds(1200);
        private readonly object _pulse = new object();
        private bool _recording;
        private bool _paused;
        private string _clock = "00:00:00";
        private string _details = string.Empty;
        private bool _blink;
        private IconKind _placeholder = IconKind.Monitor;
        private string? _caption;

        public PreviewBox()
        {
            AccessibleRole = AccessibleRole.Graphic;
            AccessibleName = "Xem trước màn hình sẽ ghi";
        }

        public Image? Image
        {
            get => _image;
            set
            {
                var old = _image;
                _image = value;
                old?.Dispose();
                Invalidate();
            }
        }

        /// <summary>What to draw when there is no screen picture (webcam and sound-only modes).</summary>
        public void SetPlaceholder(IconKind icon, string? caption)
        {
            _placeholder = icon;
            _caption = caption;
            Invalidate();
        }

        public void ShowRecording(bool recording, bool paused, string clock, string details)
        {
            _recording = recording;
            _paused = paused;
            _clock = clock;
            _details = details;
            _blink = !_blink;
            AccessibleName = recording ? (paused ? "Tạm dừng " : "Đang ghi ") + clock : "Xem trước màn hình sẽ ghi";
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _image?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var box = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            var radius = 12 * Dpi;

            using (var clip = Theme.RoundedRect(box, radius))
            {
                g.SetClip(clip);
                if (_recording)
                {
                    using (var brush = new LinearGradientBrush(box, Theme.StageTop, Theme.StageBottom, 60f))
                    {
                        g.FillRectangle(brush, box);
                    }

                    PaintClock(g, box);
                }
                else if (_image != null)
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(_image, box);
                }
                else
                {
                    using (var brush = new LinearGradientBrush(box, Theme.AccentSoft, Theme.Background, 90f))
                    {
                        g.FillRectangle(brush, box);
                    }

                    var icon = 40 * Dpi;
                    var top = box.Y + (box.Height - icon) / 2 - (_caption == null ? 0 : 12 * Dpi);
                    Icons.Draw(g, _placeholder, new RectangleF(box.X + (box.Width - icon) / 2, top, icon, icon), Theme.Accent);
                    if (_caption != null)
                    {
                        TextRenderer.DrawText(g, _caption, Theme.Small, new Rectangle((int)box.X, (int)(top + icon + 8 * Dpi), (int)box.Width, (int)(20 * Dpi)), Theme.Muted,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                    }
                }

                g.ResetClip();
            }

            Theme.StrokeRounded(g, _recording ? Theme.StageEdge : Theme.Border, 1f * Dpi, box, radius);
        }

        private void PaintClock(Graphics g, RectangleF box)
        {
            var color = _paused ? Theme.Pause : Theme.Record;
            var dot = 12 * Dpi;
            var label = _paused ? "TẠM DỪNG" : "ĐANG GHI";
            var labelSize = TextRenderer.MeasureText(label, Theme.BodyBold);
            var rowWidth = dot + 8 * Dpi + labelSize.Width;
            var top = box.Y + box.Height * 0.22f;
            var x = box.X + (box.Width - rowWidth) / 2;

            // The dot breathes while recording and blinks softly while paused (a hard on/off without animations).
            if (_recording && !Animator.IsLooping(_pulse))
            {
                Animator.Loop(this, _pulse, () => _recording);
            }

            var beat = Animator.Enabled ? (float)Easing.Pulse(Animator.Now, Blink) : (_blink ? 1f : 0f);
            var opacity = _paused ? beat : 1f;
            var spread = (3 + 4 * (_paused ? 0 : beat)) * Dpi;
            if (opacity > 0.02f)
            {
                using (var halo = new SolidBrush(Color.FromArgb((int)((70 - 40 * (_paused ? 0 : beat)) * opacity), color)))
                using (var fill = new SolidBrush(Theme.Fade(color, opacity)))
                {
                    g.FillEllipse(halo, x - spread, top + (labelSize.Height - dot) / 2 - spread, dot + spread * 2, dot + spread * 2);
                    g.FillEllipse(fill, x, top + (labelSize.Height - dot) / 2, dot, dot);
                }
            }

            TextRenderer.DrawText(g, label, Theme.BodyBold, new Point((int)(x + dot + 8 * Dpi), (int)top), Color.FromArgb(230, Color.White), TextFormatFlags.NoPadding);

            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var clockTop = top + labelSize.Height + 6 * Dpi;
            var clockHeight = TextRenderer.MeasureText(_clock, Theme.Clock).Height;
            TextRenderer.DrawText(g, _clock, Theme.Clock, new Rectangle((int)box.X, (int)clockTop, (int)box.Width, clockHeight), Color.White, flags);
            TextRenderer.DrawText(g, _details, Theme.Small, new Rectangle((int)box.X, (int)(clockTop + clockHeight + 4 * Dpi), (int)box.Width, (int)(20 * Dpi)), Color.FromArgb(190, Color.White), flags | TextFormatFlags.EndEllipsis);
        }
    }

    internal sealed record MonitorTile(string DeviceName, int Number, Rectangle Bounds, bool IsPrimary);

    /// <summary>A horizontal sound level bar: green, then amber, red near clipping; falls back smoothly.</summary>
    internal sealed class LevelMeter : Widget
    {
        private readonly Tween _shown = new Tween(0);
        private float _level;
        private float _peak;

        public LevelMeter()
        {
            AccessibleRole = AccessibleRole.ProgressBar;
        }

        public bool Active { get; set; } = true;

        /// <summary>Linear peak 0..1 from the device; shown on a dB-like curve so speech isn't a thin sliver.</summary>
        public void Push(float peak)
        {
            var shown = peak <= 0.0001f ? 0f : (float)Math.Max(0, 1 + Math.Log10(peak) / 2.4);
            _level = shown > _level ? shown : Math.Max(shown, _level - 0.06f);
            _peak = _level > _peak ? _level : Math.Max(_level, _peak - 0.01f);

            // Readings arrive ten times a second; the bar glides between them instead of stepping.
            Animator.Animate(this, _shown, _level, 90);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var bar = new RectangleF(0, (Height - 6 * Dpi) / 2, Width - 1, 6 * Dpi);
            Theme.FillRounded(g, Theme.Background, bar, 3 * Dpi);
            var level = (float)_shown.Value;
            if (!Active || level <= 0.001f)
            {
                return;
            }

            var filled = new RectangleF(bar.X, bar.Y, bar.Width * Math.Min(1f, level), bar.Height);
            using (var path = Theme.RoundedRect(filled, 3 * Dpi))
            using (var brush = new LinearGradientBrush(bar, Theme.Ok, Theme.Record, 0f))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { Theme.Ok, Theme.Ok, Theme.Pause, Theme.Record },
                    Positions = new[] { 0f, 0.65f, 0.85f, 1f },
                };
                g.FillPath(brush, path);
            }

            var peakX = bar.X + bar.Width * Math.Min(1f, _peak);
            using (var pen = new Pen(Theme.Text, 1.5f * Dpi))
            {
                g.DrawLine(pen, peakX, bar.Y - 1 * Dpi, peakX, bar.Bottom + 1 * Dpi);
            }
        }
    }

    /// <summary>A rounded pill with a short value ("60 fps", "H.264"), for the settings summary.</summary>
    internal sealed class Pill : Widget
    {
        private readonly bool _strong;

        public Pill(string text, bool strong = false)
        {
            Text = text;
            _strong = strong;
            AccessibleRole = AccessibleRole.StaticText;
            AccessibleName = text;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = TextRenderer.MeasureText(Text, Theme.Small);
            return new Size(text.Width + S(18), text.Height + S(8));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            var box = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Theme.FillRounded(g, _strong ? Theme.AccentStrong : Theme.AccentSoft, box, box.Height / 2);
            TextRenderer.DrawText(g, Text, Theme.Small, Rectangle.Round(box), _strong ? Color.White : Theme.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>Free space on the drive Recora saves to, at the foot of the sidebar.</summary>
    internal sealed class DiskGauge : Widget
    {
        private string _label = string.Empty;
        private float _used;
        private bool _low;

        public DiskGauge()
        {
            BackColor = Theme.Sidebar;
            AccessibleRole = AccessibleRole.StaticText;
        }

        public void SetUsage(string label, float usedFraction, bool low)
        {
            _label = label;
            _used = Math.Max(0, Math.Min(1, usedFraction));
            _low = low;
            AccessibleName = label;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Smooth(g);
            if (string.IsNullOrEmpty(_label))
            {
                return;
            }

            var x = S(16);
            var width = Width - S(32);
            var textHeight = TextRenderer.MeasureText(_label, Theme.Small).Height;
            var top = (Height - textHeight - S(12)) / 2;
            TextRenderer.DrawText(g, _label, Theme.Small, new Rectangle(x, top, width, textHeight), _low ? Theme.Record : Theme.Muted, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            var bar = new RectangleF(x, top + textHeight + S(6), width, S(5));
            Theme.FillRounded(g, Theme.Surface, bar, bar.Height / 2);
            Theme.FillRounded(g, _low ? Theme.Record : Theme.Accent, new RectangleF(bar.X, bar.Y, Math.Max(bar.Height, bar.Width * _used), bar.Height), bar.Height / 2);
        }
    }
}

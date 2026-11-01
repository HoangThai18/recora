using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Recora.App.Logging;
using Recora.App.Recording;
using Recora.App.UI.Controls;
using Recora.Core;
using Recora.Core.Ffmpeg;
using Recora.Core.Library;

namespace Recora.App.UI
{
    /// <summary>
    /// "Cắt video": two sliders for the start and the end, a still of whichever one moved, and a save that writes
    /// "&lt;name&gt; (cắt)" next to the source by stream copy. The original file stays as it is.
    /// </summary>
    internal sealed class TrimDialog : Form
    {
        private const int TicksPerSecond = 10;
        private readonly string? _ffmpeg;
        private readonly string _source;
        private readonly TimeSpan _duration;
        private readonly PictureBox _preview;
        private readonly TrackBar _start;
        private readonly TrackBar _end;
        private readonly Label _startText;
        private readonly Label _endText;
        private readonly Label _range;
        private readonly FlatButton _save;
        private readonly FlatButton _cancel;
        private readonly Timer _debounce;
        private TimeSpan _pending;
        private int _generation;
        private bool _saving;

        /// <param name="ffmpeg">Null renders the dialog without asking FFmpeg for stills (UI screenshots).</param>
        public TrimDialog(string? ffmpeg, LibraryItem item, TimeSpan duration)
        {
            _ffmpeg = ffmpeg;
            _source = item.Path;
            _duration = duration;

            var dpi = DeviceDpi / 96f * Theme.UiScale;
            int S(float v) => (int)Math.Round(v * dpi);

            Text = "Cắt video · " + item.Name;
            Font = Theme.Body;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Icon = AppIcon.Large;

            var layout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(S(20)),
                BackColor = Theme.Background,
            };

            var width = S(FramePreviewCommand.Width);
            _preview = new PictureBox
            {
                Size = new Size(width, S(FramePreviewCommand.Height)),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Theme.FromHex(0x12304F),
                Margin = new Padding(0, 0, 0, S(14)),
            };

            var ticks = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds * TicksPerSecond));
            _start = Slider(ticks, 0, width);
            _end = Slider(ticks, ticks, width);
            _startText = Label(Theme.BodyBold, Theme.Text, S(2));
            _endText = Label(Theme.BodyBold, Theme.Text, S(2));
            _range = Label(Theme.Mono, Theme.Accent, S(8));
            var note = Label(Theme.Small, Theme.Muted, S(16));
            note.MaximumSize = new Size(width, 0);
            note.Text = "Cắt nhanh, giữ nguyên chất lượng. Điểm đầu có thể lùi tối đa 2 giây về khung hình khoá gần nhất. "
                + "File gốc vẫn được giữ, bản cắt nằm cạnh nó.";

            _save = new FlatButton("Lưu bản cắt") { Primary = true, Size = new Size(S(150), S(36)), Margin = new Padding(0, 0, S(10), 0) };
            _cancel = new FlatButton("Huỷ") { Size = new Size(S(100), S(36)), Margin = Padding.Empty };
            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Theme.Background, Margin = Padding.Empty };
            buttons.Controls.AddRange(new Control[] { _save, _cancel });

            layout.Controls.AddRange(new Control[] { _preview, _startText, _start, _endText, _end, _range, note, buttons });
            Controls.Add(layout);
            ClientSize = layout.GetPreferredSize(Size.Empty);
            AcceptButton = null;

            _debounce = new Timer { Interval = 150 };
            _debounce.Tick += (_, _) =>
            {
                _debounce.Stop();
                ShowFrame(_pending);
            };

            _start.Scroll += (_, _) =>
            {
                if (_start.Value > _end.Value - TicksPerSecond)
                {
                    _start.Value = Math.Max(0, _end.Value - TicksPerSecond);
                }

                Changed(Start);
            };
            _end.Scroll += (_, _) =>
            {
                if (_end.Value < _start.Value + TicksPerSecond)
                {
                    _end.Value = Math.Min(_end.Maximum, _start.Value + TicksPerSecond);
                }

                Changed(End);
            };
            _save.Click += async (_, _) => await SaveAsync();
            _cancel.Click += (_, _) => Close();

            UpdateTexts();
        }

        /// <summary>The cut that was written, once the dialog closes with OK.</summary>
        public string? SavedPath { get; private set; }

        private TimeSpan Start => TimeSpan.FromSeconds(_start.Value / (double)TicksPerSecond);

        private TimeSpan End => _end.Value >= _end.Maximum ? _duration : TimeSpan.FromSeconds(_end.Value / (double)TicksPerSecond);

        /// <summary>For UI screenshots: a picture instead of a decoded still, and the sliders somewhere in the middle.</summary>
        public void ShowSample(Image? image, TimeSpan from, TimeSpan to)
        {
            _start.Value = (int)(from.TotalSeconds * TicksPerSecond);
            _end.Value = Math.Min(_end.Maximum, (int)(to.TotalSeconds * TicksPerSecond));
            _preview.Image = image;
            UpdateTexts();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ShowFrame(TimeSpan.Zero);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // A save in progress finishes: closing then would leave a half-written cut.
            if (_saving)
            {
                e.Cancel = true;
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _debounce.Dispose();
                _preview.Image?.Dispose();
            }

            base.Dispose(disposing);
        }

        private static TrackBar Slider(int ticks, int value, int width) => new TrackBar
        {
            Minimum = 0,
            Maximum = ticks,
            Value = value,
            TickStyle = TickStyle.None,
            SmallChange = 1,
            LargeChange = 5 * TicksPerSecond,
            Width = width,
            BackColor = Theme.Background,
            Margin = new Padding(0, 0, 0, 6),
        };

        private static Label Label(Font font, Color color, int bottom) => new Label
        {
            AutoSize = true,
            Font = font,
            ForeColor = color,
            BackColor = Theme.Background,
            Margin = new Padding(0, 0, 0, bottom),
        };

        private static string Clock(TimeSpan value) =>
            value.TotalHours >= 1
                ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds / 100}"
                : $"{(int)value.TotalMinutes}:{value.Seconds:00}.{value.Milliseconds / 100}";

        private void Changed(TimeSpan at)
        {
            UpdateTexts();
            _pending = at;
            _debounce.Stop();
            _debounce.Start();
        }

        private void UpdateTexts()
        {
            _startText.Text = "Điểm đầu · " + Clock(Start);
            _endText.Text = "Điểm cuối · " + Clock(End);
            _range.Text = $"> giữ {Clock(End - Start)} trên tổng {Clock(_duration)}";
        }

        /// <summary>Asks FFmpeg for a still off the UI thread; a newer request makes an older answer irrelevant.</summary>
        private async void ShowFrame(TimeSpan at)
        {
            var ffmpeg = _ffmpeg;
            if (ffmpeg == null)
            {
                return;
            }

            var generation = ++_generation;
            Bitmap? frame;
            try
            {
                frame = await Task.Run(() => TrimService.Frame(ffmpeg, _source, at));
            }
            catch (Exception error) when (error is IOException || error is InvalidOperationException || error is System.ComponentModel.Win32Exception)
            {
                Log.Warn("preview frame failed: " + error.Message);
                frame = null;
            }

            if (IsDisposed || generation != _generation)
            {
                frame?.Dispose();
                return;
            }

            var old = _preview.Image;
            _preview.Image = frame ?? Placeholder();
            old?.Dispose();
        }

        private Bitmap Placeholder()
        {
            var bitmap = new Bitmap(_preview.Width, _preview.Height);
            using (var g = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
            {
                g.Clear(Theme.FromHex(0x12304F));
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("Không xem trước được video này\n(vẫn cắt được)", Theme.Body, brush, new RectangleF(0, 0, bitmap.Width, bitmap.Height), format);
            }

            return bitmap;
        }

        private async Task SaveAsync()
        {
            var ffmpeg = _ffmpeg;
            if (ffmpeg == null)
            {
                return;
            }

            var from = Start;
            var to = End;
            _saving = true;
            _save.Enabled = false;
            _cancel.Enabled = false;
            _start.Enabled = false;
            _end.Enabled = false;
            _save.Text = "Đang lưu…";
            try
            {
                SavedPath = await Task.Run(() => TrimService.Cut(ffmpeg, _source, from, to));
                _saving = false;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception error) when (error is RecoraException || error is IOException || error is UnauthorizedAccessException)
            {
                Log.Error("trim failed", error);
                _saving = false;
                _save.Text = "Lưu bản cắt";
                _save.Enabled = true;
                _cancel.Enabled = true;
                _start.Enabled = true;
                _end.Enabled = true;
                MessageBox.Show(this, "Không cắt được video này. Thử lại, hoặc xem nhật ký ở trang Giới thiệu.", "Cắt video", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}

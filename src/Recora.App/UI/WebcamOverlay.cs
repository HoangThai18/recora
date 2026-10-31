using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Recora.App.Logging;
using Recora.App.Native;
using Recora.Core.Ffmpeg;

namespace Recora.App.UI
{
    /// <summary>
    /// "Webcam nổi": a small rounded window with the live webcam in a corner of the recorded screen, draggable. FFmpeg
    /// decodes the camera to raw BGRA on stdout; this window is captured with the screen like any other.
    /// </summary>
    internal sealed class WebcamOverlay : Form
    {
        private readonly Process _ffmpeg;
        private readonly Thread _reader;
        private readonly Bitmap _frame = new Bitmap(WebcamPreviewCommand.Width, WebcamPreviewCommand.Height, PixelFormat.Format32bppArgb);
        private readonly object _gate = new object();
        private volatile bool _closing;
        private bool _disposed;
        private Point _dragFrom;

        public WebcamOverlay(string ffmpeg, string device, Rectangle screen)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            BackColor = Theme.FromHex(0x13263D);
            Cursor = Cursors.SizeAll;

            // A quarter of the recorded area's width at most, bottom-right with a margin.
            var scale = DeviceDpi / 96f;
            var width = Math.Min((int)(WebcamPreviewCommand.Width * scale), Math.Max(80, screen.Width / 4));
            var size = new Size(width, width * WebcamPreviewCommand.Height / WebcamPreviewCommand.Width);
            var margin = Math.Min((int)(24 * scale), screen.Width / 20);
            Bounds = new Rectangle(screen.Right - size.Width - margin, screen.Bottom - size.Height - margin, size.Width, size.Height);
            using (var path = Theme.RoundedRect(new RectangleF(0, 0, size.Width, size.Height), 16 * scale))
            {
                Region = new Region(path);
            }

            var start = new ProcessStartInfo(ffmpeg, WebcamPreviewCommand.Build(device, mirror: true).ToCommandLine())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            _ffmpeg = Process.Start(start)!;
            ChildProcessJob.Attach(_ffmpeg);
            _ffmpeg.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Log.Warn("webcam overlay: " + e.Data);
                }
            };
            _ffmpeg.BeginErrorReadLine();
            _reader = new Thread(ReadFrames) { IsBackground = true, Name = "webcam overlay" };
            _reader.Start();
        }

        protected override bool ShowWithoutActivation => true;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _dragFrom = e.Location;
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Location = new Point(Location.X + e.X - _dragFrom.X, Location.Y + e.Y - _dragFrom.Y);
            }

            base.OnMouseMove(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            lock (_gate)
            {
                if (!_disposed)
                {
                    e.Graphics.DrawImage(_frame, ClientRectangle);
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _closing = true;
            try
            {
                if (!_ffmpeg.HasExited)
                {
                    _ffmpeg.Kill();
                }
            }
            catch (InvalidOperationException)
            {
            }

            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _closing = true;
                try
                {
                    if (!_ffmpeg.HasExited)
                    {
                        _ffmpeg.Kill();
                    }
                }
                catch (InvalidOperationException)
                {
                }

                // The reader writes into the bitmap under the gate: stop it before freeing what it writes to.
                _reader.Join(TimeSpan.FromSeconds(2));
                lock (_gate)
                {
                    _disposed = true;
                    _frame.Dispose();
                }

                _ffmpeg.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ReadFrames()
        {
            var buffer = new byte[WebcamPreviewCommand.FrameBytes];
            var stream = _ffmpeg.StandardOutput.BaseStream;
            try
            {
                while (!_closing)
                {
                    var read = 0;
                    while (read < buffer.Length)
                    {
                        var n = stream.Read(buffer, read, buffer.Length - read);
                        if (n <= 0)
                        {
                            return;
                        }

                        read += n;
                    }

                    lock (_gate)
                    {
                        if (_disposed)
                        {
                            return;
                        }

                        var bits = _frame.LockBits(new Rectangle(0, 0, _frame.Width, _frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                        try
                        {
                            // BGRA from FFmpeg already matches GDI+'s 32bpp layout; force opaque alpha.
                            for (var i = 3; i < buffer.Length; i += 4)
                            {
                                buffer[i] = 255;
                            }

                            Marshal.Copy(buffer, 0, bits.Scan0, buffer.Length);
                        }
                        finally
                        {
                            _frame.UnlockBits(bits);
                        }
                    }

                    if (!_closing && IsHandleCreated)
                    {
                        BeginInvoke(new Action(Invalidate));
                    }
                }
            }
            catch (Exception error) when (error is IOException || error is ObjectDisposedException || error is InvalidOperationException || error is ArgumentException)
            {
                Log.Warn("webcam overlay stopped: " + error.Message);
            }
        }
    }
}

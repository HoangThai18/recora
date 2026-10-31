using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Recora.App.Native;

namespace Recora.App.UI
{
    /// <summary>
    /// A dashed frame just outside the recorded region, so the user sees what is being captured. It lets clicks through,
    /// never takes focus, and is excluded from capture, so it can't end up in the video either way.
    /// </summary>
    internal sealed class RegionFrame : Form
    {
        private const int Thickness = 4;
        private const int WsExLayered = 0x80000;
        private const int WsExTransparent = 0x20;
        private const int WsExToolWindow = 0x80;
        private const int WsExNoActivate = 0x08000000;
        private bool _recording = true;

        public RegionFrame(Rectangle region)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            Bounds = Rectangle.Inflate(region, Thickness, Thickness);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate;
                return parameters;
            }
        }

        /// <summary>Red while recording, amber while paused.</summary>
        public void SetRecording(bool recording)
        {
            _recording = recording;
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowExclusion.Apply(this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var pen = new Pen(_recording ? Theme.Record : Theme.Pause, 2f) { DashStyle = DashStyle.Dash })
            {
                e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
        }
    }
}

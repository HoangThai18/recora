using System;
using Recora.Core.Settings;

namespace Recora.Core.Capture
{
    /// <summary>Where the recording bar goes on a screen: a corner, a margin in from the edges, never off the screen.</summary>
    public static class OverlayPlacement
    {
        public static PixelRect Place(PixelRect screen, int width, int height, OverlayCorner corner, int margin)
        {
            width = Math.Max(1, Math.Min(width, screen.Width));
            height = Math.Max(1, Math.Min(height, screen.Height));
            var spareX = screen.Width - width;
            var spareY = screen.Height - height;
            var insetX = Math.Max(0, Math.Min(margin, spareX / 2));
            var insetY = Math.Max(0, Math.Min(margin, spareY / 2));
            var left = corner == OverlayCorner.TopLeft || corner == OverlayCorner.BottomLeft;
            var top = corner == OverlayCorner.TopLeft || corner == OverlayCorner.TopRight;
            var x = left ? screen.X + insetX : screen.Right - width - insetX;
            var y = top ? screen.Y + insetY : screen.Bottom - height - insetY;
            return new PixelRect(x, y, width, height);
        }
    }
}

using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Recora.App.UI
{
    /// <summary>
    /// A small picture of a screen for the home page, scaled down by GDI in one StretchBlt (cheap even for 4K).
    /// Windows hidden from capture, Recora's own included, are left out just as they will be in the video.
    /// </summary>
    internal static class ScreenPreview
    {
        private const int Halftone = 4;
        private const int SrcCopy = 0x00CC0020;

        public static Bitmap? Capture(Rectangle screen, Size size)
        {
            if (screen.Width <= 0 || screen.Height <= 0 || size.Width <= 0 || size.Height <= 0)
            {
                return null;
            }

            var bitmap = new Bitmap(size.Width, size.Height);
            var source = GetDC(IntPtr.Zero);
            try
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    var target = g.GetHdc();
                    try
                    {
                        SetStretchBltMode(target, Halftone);
                        SetBrushOrgEx(target, 0, 0, IntPtr.Zero);
                        if (!StretchBlt(target, 0, 0, size.Width, size.Height, source, screen.X, screen.Y, screen.Width, screen.Height, SrcCopy))
                        {
                            bitmap.Dispose();
                            return null;
                        }
                    }
                    finally
                    {
                        g.ReleaseHdc(target);
                    }
                }

                return bitmap;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, source);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern int SetStretchBltMode(IntPtr hdc, int mode);

        [DllImport("gdi32.dll")]
        private static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr previous);

        [DllImport("gdi32.dll")]
        private static extern bool StretchBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int sx, int sy, int sw, int sh, int rop);
    }
}

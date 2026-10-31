using System;

namespace Recora.Core.Capture
{
    /// <summary>A rectangle in physical pixels (the app is per-monitor DPI aware, so these match what FFmpeg sees).</summary>
    public readonly record struct PixelRect(int X, int Y, int Width, int Height)
    {
        /// <summary>Smallest frame every encoder accepts (NVENC refuses tiny frames); see BR-07.</summary>
        public const int MinWidth = 160;

        public const int MinHeight = 90;

        public int Right => X + Width;

        public int Bottom => Y + Height;

        public bool IsEmpty => Width <= 0 || Height <= 0;

        public static PixelRect FromEdges(int left, int top, int right, int bottom) =>
            new PixelRect(left, top, right - left, bottom - top);

        public PixelRect Intersect(PixelRect other)
        {
            var left = Math.Max(X, other.X);
            var top = Math.Max(Y, other.Y);
            var right = Math.Min(Right, other.Right);
            var bottom = Math.Min(Bottom, other.Bottom);
            return right > left && bottom > top ? FromEdges(left, top, right, bottom) : default;
        }

        public PixelRect Offset(int dx, int dy) => new PixelRect(X + dx, Y + dy, Width, Height);

        /// <summary>
        /// Rounds width and height down to even numbers (4:2:0 video needs them) and rejects frames below
        /// <see cref="MinWidth"/> x <see cref="MinHeight"/>.
        /// </summary>
        public PixelRect ToEncodable()
        {
            var even = new PixelRect(X, Y, Width & ~1, Height & ~1);

            if (even.Width < MinWidth || even.Height < MinHeight)
            {
                throw new RecoraException(
                    "region_too_small",
                    $"capture area {Width}x{Height} is below {MinWidth}x{MinHeight}");
            }

            return even;
        }

        public override string ToString() => $"{Width}x{Height}+{X}+{Y}";
    }
}

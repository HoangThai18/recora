using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Recora.App.UI
{
    internal enum IconKind
    {
        Monitor,
        Region,
        Window,
        Game,
        Webcam,
        Audio,
        Record,
        Stop,
        Pause,
        Camera,
        Home,
        Video,
        Speaker,
        Mic,
        Keyboard,
        Sliders,
        Library,
        Info,
        Folder,
        Film,
    }

    /// <summary>
    /// Line icons drawn on a 24-unit grid (Lucide-style) and scaled into any box, so the exe needs no image files and
    /// stays sharp at every DPI.
    /// </summary>
    internal static class Icons
    {
        public static void Draw(Graphics g, IconKind kind, RectangleF box, Color color)
        {
            var size = Math.Min(box.Width, box.Height);
            var unit = size / 24f;
            var ox = box.X + (box.Width - size) / 2;
            var oy = box.Y + (box.Height - size) / 2;

            PointF P(float x, float y) => new PointF(ox + x * unit, oy + y * unit);
            RectangleF R(float x, float y, float w, float h) => new RectangleF(ox + x * unit, oy + y * unit, w * unit, h * unit);

            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var pen = new Pen(color, Math.Max(1.2f, 1.9f * unit)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var brush = new SolidBrush(color))
            {
                void Line(float x1, float y1, float x2, float y2) => g.DrawLine(pen, P(x1, y1), P(x2, y2));
                void Poly(params float[] xy)
                {
                    var points = new PointF[xy.Length / 2];
                    for (var i = 0; i < points.Length; i++)
                    {
                        points[i] = P(xy[i * 2], xy[i * 2 + 1]);
                    }

                    g.DrawLines(pen, points);
                }

                void Rounded(float x, float y, float w, float h, float r)
                {
                    using (var path = Theme.RoundedRect(R(x, y, w, h), r * unit))
                    {
                        g.DrawPath(pen, path);
                    }
                }

                void Circle(float cx, float cy, float r) => g.DrawEllipse(pen, R(cx - r, cy - r, r * 2, r * 2));
                void Dot(float cx, float cy, float r) => g.FillEllipse(brush, R(cx - r, cy - r, r * 2, r * 2));

                switch (kind)
                {
                    case IconKind.Monitor:
                        Rounded(2, 3, 20, 14, 2);
                        Line(8, 21, 16, 21);
                        Line(12, 17, 12, 21);
                        break;
                    case IconKind.Region:
                        Poly(3, 8, 3, 3, 8, 3);
                        Poly(16, 3, 21, 3, 21, 8);
                        Poly(21, 16, 21, 21, 16, 21);
                        Poly(8, 21, 3, 21, 3, 16);
                        Dot(12, 12, 1.6f);
                        break;
                    case IconKind.Window:
                        Rounded(3, 4, 18, 16, 2);
                        Line(3, 9, 21, 9);
                        Dot(6, 6.5f, 0.9f);
                        Dot(8.8f, 6.5f, 0.9f);
                        break;
                    case IconKind.Game:
                        Rounded(2, 7, 20, 11, 5);
                        Line(6, 12.5f, 10, 12.5f);
                        Line(8, 10.5f, 8, 14.5f);
                        Dot(15.5f, 11.5f, 1.1f);
                        Dot(18, 13.8f, 1.1f);
                        break;
                    case IconKind.Webcam:
                        Circle(12, 10, 7);
                        Circle(12, 10, 2.8f);
                        Line(12, 17, 12, 20);
                        Line(8, 21, 16, 21);
                        break;
                    case IconKind.Audio:
                        Line(4, 10, 4, 14);
                        Line(8, 6, 8, 18);
                        Line(12, 3, 12, 21);
                        Line(16, 7, 16, 17);
                        Line(20, 10, 20, 14);
                        break;
                    case IconKind.Record:
                        Dot(12, 12, 8);
                        break;
                    case IconKind.Stop:
                        using (var path = Theme.RoundedRect(R(5, 5, 14, 14), 2.5f * unit))
                        {
                            g.FillPath(brush, path);
                        }

                        break;
                    case IconKind.Pause:
                        using (var left = Theme.RoundedRect(R(6, 4.5f, 4, 15), 1.2f * unit))
                        using (var right = Theme.RoundedRect(R(14, 4.5f, 4, 15), 1.2f * unit))
                        {
                            g.FillPath(brush, left);
                            g.FillPath(brush, right);
                        }

                        break;
                    case IconKind.Camera:
                        Rounded(2, 6, 20, 14, 2.5f);
                        Poly(8, 6, 9.5f, 3.5f, 14.5f, 3.5f, 16, 6);
                        Circle(12, 13, 3.6f);
                        break;
                    case IconKind.Home:
                        Poly(3, 11, 12, 3.5f, 21, 11);
                        Poly(5.5f, 9.5f, 5.5f, 20.5f, 18.5f, 20.5f, 18.5f, 9.5f);
                        Line(10, 20.5f, 10, 15);
                        Poly(10, 15, 14, 15, 14, 20.5f);
                        break;
                    case IconKind.Video:
                        Rounded(2, 6, 14, 12, 2);
                        Poly(16, 10, 22, 7, 22, 17, 16, 14);
                        break;
                    case IconKind.Film:
                        // A clapperboard: the slate below, the hinged stick with two stripes above.
                        Rounded(3, 10, 18, 11, 2);
                        Poly(3, 10, 2.4f, 6.4f, 18.8f, 2.2f, 19.7f, 5.8f, 3, 10);
                        Line(7.6f, 5.1f, 9.6f, 8.5f);
                        Line(12.8f, 3.8f, 14.8f, 7.2f);
                        break;
                    case IconKind.Speaker:
                        Poly(3, 9.5f, 7, 9.5f, 12, 5, 12, 19, 7, 14.5f, 3, 14.5f, 3, 9.5f);
                        g.DrawArc(pen, R(11, 8.5f, 7, 7), -55, 110);
                        g.DrawArc(pen, R(11, 5, 11, 14), -55, 110);
                        break;
                    case IconKind.Mic:
                        Rounded(9, 2, 6, 12, 3);
                        g.DrawArc(pen, R(5, 6, 14, 12), 0, 180);
                        Line(12, 18, 12, 22);
                        break;
                    case IconKind.Keyboard:
                        Rounded(2, 6, 20, 12, 2);
                        Line(6, 10, 6.01f, 10);
                        Line(10, 10, 10.01f, 10);
                        Line(14, 10, 14.01f, 10);
                        Line(18, 10, 18.01f, 10);
                        Line(7, 14, 17, 14);
                        break;
                    case IconKind.Sliders:
                        Line(4, 7, 20, 7);
                        Line(4, 17, 20, 17);
                        Dot(9, 7, 2.7f);
                        Dot(15, 17, 2.7f);
                        break;
                    case IconKind.Library:
                        Rounded(3, 4, 18, 16, 2);
                        Poly(10, 9, 15, 12, 10, 15, 10, 9);
                        break;
                    case IconKind.Info:
                        Circle(12, 12, 9.5f);
                        Line(12, 11, 12, 16.5f);
                        Dot(12, 7.8f, 1.1f);
                        break;
                    case IconKind.Folder:
                        Poly(3, 18.5f, 3, 5.5f, 9, 5.5f, 11, 8, 21, 8, 21, 18.5f, 3, 18.5f);
                        break;
                }
            }

            g.Restore(state);
        }
    }
}

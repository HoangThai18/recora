using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Recora.App.Logging;
using Recora.Core.Recording;
using Recora.Core.Settings;

namespace Recora.App.Recording
{
    internal sealed record SavedScreenshot(string Path, Size Size, bool Copied);

    /// <summary>
    /// F11: a still of what the current mode records (screen, region or window), saved next to the videos as PNG or
    /// JPG, optionally copied to the clipboard. GDI capture: instant, and Recora's own windows stay out of it.
    /// </summary>
    internal static class ScreenshotService
    {
        public static SavedScreenshot Take(Rectangle area, string folder, ScreenshotFormat format, bool copyToClipboard, bool sound)
        {
            Directory.CreateDirectory(folder);
            using (var bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(area.Location, Point.Empty, area.Size, CopyPixelOperation.SourceCopy);
                }

                var extension = format == ScreenshotFormat.Jpg ? ".jpg" : ".png";
                var path = OutputNaming.Unique(folder, OutputNaming.BaseName(DateTime.Now), extension, File.Exists);
                if (format == ScreenshotFormat.Jpg)
                {
                    SaveJpeg(bitmap, path, 92);
                }
                else
                {
                    bitmap.Save(path, ImageFormat.Png);
                }

                var copied = false;
                if (copyToClipboard)
                {
                    try
                    {
                        Clipboard.SetImage(bitmap);
                        copied = true;
                    }
                    catch (Exception error) when (error is ExternalException || error is ThreadStateException)
                    {
                        Log.Warn("clipboard busy, screenshot not copied: " + error.Message);
                    }
                }

                if (sound)
                {
                    SystemSounds.Asterisk.Play();
                }

                Log.Info($"screenshot {path} ({area.Width}x{area.Height})");
                return new SavedScreenshot(path, area.Size, copied);
            }
        }

        private static void SaveJpeg(Bitmap bitmap, string path, long quality)
        {
            var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using (var parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                bitmap.Save(path, codec, parameters);
            }
        }
    }
}

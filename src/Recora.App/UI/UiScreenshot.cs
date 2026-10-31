using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Recora.App.UI
{
    /// <summary>
    /// <c>Recora.exe --ui-screenshot out.png [--ui-scale 1.5] [--page Home]</c>: renders the main window to a PNG and exits,
    /// so CI can show what the UI looks like without anyone opening it.
    /// </summary>
    internal static class UiScreenshot
    {
        public static int Run(string path, float scale, PageKind page)
        {
            Theme.Configure(scale);

            using (var form = new MainForm())
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(20, 20);
                form.ShowInTaskbar = false;
                form.Show();
                form.ShowPage(page);
                form.Home.Display(new HomeModel(
                    "Toàn màn hình",
                    "Màn hình 1 · 1920×1080 · 60 khung/giây",
                    "NVIDIA NVENC · H.264 · ghi bằng GPU",
                    "Cân bằng · H.264 · 60 fps · MP4 · độ phân giải gốc",
                    SystemAudio: true,
                    Microphone: false));
                form.Status.SetState("Sẵn sàng", Theme.Ok);
                form.Status.Details = "F12 ghi · Shift+F12 tạm dừng · F11 chụp ảnh";
                form.Status.RightText = @"D:\Videos\Recora";

                for (var i = 0; i < 10; i++)
                {
                    Application.DoEvents();
                }

                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(path, ImageFormat.Png);
                }

                Console.WriteLine($"saved {path} ({form.Width}x{form.Height}, scale {scale})");
            }

            return 0;
        }
    }
}

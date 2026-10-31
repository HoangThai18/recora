using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Recora.App.UI.Controls;
using Recora.App.Audio;
using Recora.App.Recording;
using Recora.Core.Encoding;
using Recora.Core.Library;
using Recora.Core.Settings;

namespace Recora.App.UI
{
    /// <summary>
    /// <c>Recora.exe --ui-screenshot out.png [--ui-scale 1.5] [--page Home] [--library dir]</c>: renders the main window
    /// with default settings to a PNG and exits, so CI can show what every page looks like without anyone opening it.
    /// </summary>
    internal static class UiScreenshot
    {
        public static int Run(string path, float scale, PageKind page, string? libraryFolder, bool recording = false)
        {
            Theme.Configure(scale);
            var settings = new AppSettings { OutputFolder = libraryFolder ?? @"D:\Videos\Recora" };

            using (var form = new MainForm())
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(20, 20);
                form.ShowInTaskbar = false;

                form.SetPage(PageKind.Video, new VideoPage(settings, kind => kind != EncoderKind.Amf, "NVIDIA NVENC"));
                form.SetPage(PageKind.Audio, new AudioPage(
                    settings,
                    new[] { new AudioDeviceOption("speakers", "Loa (Realtek High Definition Audio)") },
                    new[] { new AudioDeviceOption("mic", "Micro (USB Audio Device)") }));
                form.SetPage(PageKind.Hotkeys, new HotkeysPage(settings));
                form.SetPage(PageKind.Screenshot, new ScreenshotPage(settings));
                form.SetPage(PageKind.General, new GeneralPage(settings, settings.OutputFolder));
                form.SetPage(PageKind.Library, new LibraryPage(() => LibraryFiles.Scan(libraryFolder)));
                form.SetPage(PageKind.About, new AboutPage(Program.Version, "ffmpeg version 8.1.3"));

                form.Show();
                form.ShowPage(page);
                form.Home.ShowMonitors(
                    new[]
                    {
                        new MonitorTile(@"\\.\DISPLAY1", 1, new Rectangle(0, 0, 1920, 1080), IsPrimary: true),
                        new MonitorTile(@"\\.\DISPLAY2", 2, new Rectangle(1920, -180, 2560, 1440), IsPrimary: false),
                    },
                    @"\\.\DISPLAY1");
                form.Home.Display(new HomeModel(
                    "Toàn màn hình",
                    "Màn hình 1 · 1920×1080 · 60 khung/giây",
                    "NVIDIA NVENC · H.264 · ghi bằng GPU",
                    new[] { "Cân bằng", "H.264", "60 fps", "MP4", "độ phân giải gốc", "có chuột" },
                    SystemAudio: true,
                    Microphone: true));
                form.Disk.SetUsage(@"Ổ D: còn 214,6 GB", 0.58f, low: false);
                // Pictures go on the download page: show a user's folder, not the CI runner's path.
                const string shownFolder = @"C:\Users\Ban\Videos\Recora";
                form.Status.RightText = shownFolder;
                form.PageOf<GeneralPage>(PageKind.General)?.ShowFolder(shownFolder);

                // The real desktop of the machine, scaled like the app does it.
                var screen = Screen.PrimaryScreen.Bounds;
                var preview = form.Home.Preview.Size;
                form.Home.Preview.Image = ScreenPreview.Capture(screen, new Size(preview.Width, preview.Width * screen.Height / screen.Width));

                if (recording)
                {
                    form.Home.Preview.ShowRecording(true, false, "00:12:34", "1,2 GB · 60 fps · h264_nvenc");
                    form.Record.State = RecordState.Recording;
                    form.Pause.Enabled = true;
                    form.Status.SetState("Đang ghi 00:12:34", Theme.Record);
                    form.Status.Details = "1,2 GB · 60 fps · h264_nvenc";
                    form.Text = "Recora · đang ghi 00:12:34";
                }
                else
                {
                    form.Status.SetState("Sẵn sàng", Theme.Ok);
                    form.Status.Details = "F12 ghi · Shift+F12 tạm dừng · F11 chụp ảnh";
                }

                for (var level = 0; level < 12; level++)
                {
                    form.Home.SystemLevel.Push(0.45f);
                    form.Home.MicrophoneLevel.Push(0.12f);
                }

                for (var i = 0; i < 10; i++)
                {
                    Application.DoEvents();
                }

                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(path, ImageFormat.Png);
                }

                Console.WriteLine($"saved {path} ({form.Width}x{form.Height}, scale {scale}, page {page})");
            }

            return 0;
        }
    }
}

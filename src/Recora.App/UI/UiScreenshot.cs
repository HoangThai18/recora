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
        public static int Run(string path, float scale, PageKind page, string? libraryFolder, bool recording = false, string? dialog = null, bool demo = false)
        {
            Theme.Configure(scale);

            // Pictures show where things end up, not a frame caught mid-motion; the demo video is about the motion.
            Animator.Off = !demo;
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
                var edit = new EditPage();
                edit.ShowIntro("Tải Clipora và mở (khoảng 70 MB)", "Máy này chưa có Clipora.", busy: false);
                form.SetPage(PageKind.Edit, edit);

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

                const string replayHint = "Alt+F10 (cả khi đang chơi): lưu 30 giây vừa qua của màn hình đang chọn. F12 vẫn ghi thường.";
                form.Home.Replay.Checked = true;
                form.Home.ReplayLength.Value = 30;
                form.Home.ShowReplay(replayHint, recording ? "> tạm nghỉ trong lúc ghi thường" : "> đang giữ 0:30 / 30 giây · h264_nvenc", canSave: !recording);
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
                    form.Status.SetState("Đang giữ khoảnh khắc", Theme.Ok);
                    form.Status.Details = "Alt+F10 lưu 30 giây vừa qua · F12 ghi thường";
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

                if (demo)
                {
                    return Demo(form, path);
                }

                if (dialog == "trim")
                {
                    // The cut dialog over the library, with a picture of the desktop in place of a decoded still.
                    var item = new LibraryItem(@"C:\Users\Ban\Videos\Recora\Recora 2026-11-01 20-15-03 khoảnh khắc.mp4", "Recora 2026-11-01 20-15-03 khoảnh khắc.mp4", 48_000_000, DateTime.Now, TimeSpan.FromSeconds(75), false);
                    using (var trim = new TrimDialog(null, item, TimeSpan.FromSeconds(75)))
                    {
                        trim.StartPosition = FormStartPosition.Manual;
                        trim.Location = new Point(form.Left + 80, form.Top + 80);
                        trim.ShowSample(ScreenPreview.Capture(screen, new Size(480, 270)), TimeSpan.FromSeconds(12.5), TimeSpan.FromSeconds(47));
                        trim.Show(form);
                        for (var i = 0; i < 10; i++)
                        {
                            Application.DoEvents();
                        }

                        using (var bitmap = new Bitmap(trim.Width, trim.Height))
                        {
                            trim.DrawToBitmap(bitmap, new Rectangle(Point.Empty, trim.Size));
                            bitmap.Save(path, ImageFormat.Png);
                        }

                        Console.WriteLine($"saved {path} ({trim.Width}x{trim.Height}, scale {scale}, trim dialog)");
                    }

                    return 0;
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

        /// <summary>
        /// <c>--ui-demo out.mp4</c>: films the window while a script flips a switch, slides the length picker, changes
        /// pages and starts, pauses and stops a (pretend) recording, so the motion can be seen without a Windows PC.
        /// </summary>
        private static int Demo(MainForm form, string path)
        {
            var ffmpeg = Recording.FfmpegLocator.Find();
            var bounds = form.Bounds;
            var area = $"-offset_x {bounds.X} -offset_y {bounds.Y} -video_size {bounds.Width & ~1}x{bounds.Height & ~1}";
            var capture = new System.Diagnostics.ProcessStartInfo(
                ffmpeg,
                $"-hide_banner -loglevel error -y -f gdigrab -framerate 30 {area} -i desktop -t 11 -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p \"{path}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var steps = new List<(double At, Action Do)>
            {
                (0.8, () => form.Home.Replay.Checked = false),
                (1.6, () => form.Home.Replay.Checked = true),
                (2.4, () => form.Home.ReplayLength.Value = 60),
                (3.0, () => form.Home.ReplayLength.Value = 300),
                (3.6, () => form.Home.ReplayLength.Value = 15),
                (4.4, () => form.ShowPage(PageKind.Video)),
                (5.4, () => form.ShowPage(PageKind.Audio)),
                (6.4, () => form.ShowPage(PageKind.Home)),
                (7.0, () =>
                {
                    form.Record.State = RecordState.Recording;
                    form.Status.SetState("Đang ghi 00:00:01", Theme.Record);
                    form.Home.Preview.ShowRecording(true, false, "00:00:01", "12 MB · 60 fps · h264_nvenc");
                }),
                (8.8, () =>
                {
                    form.Record.State = RecordState.Paused;
                    form.Status.SetState("Tạm dừng 00:00:02", Theme.Pause);
                    form.Home.Preview.ShowRecording(true, true, "00:00:02", "24 MB · Shift+F12 để ghi tiếp");
                }),
                (10.2, () =>
                {
                    form.Record.State = RecordState.Idle;
                    form.Status.SetState("Sẵn sàng", Theme.Ok);
                    form.Home.Preview.ShowRecording(false, false, "00:00:00", string.Empty);
                }),
            };

            using (var process = System.Diagnostics.Process.Start(capture)!)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var next = 0;
                while (!process.HasExited && clock.Elapsed.TotalSeconds < 14)
                {
                    while (next < steps.Count && clock.Elapsed.TotalSeconds >= steps[next].At)
                    {
                        steps[next++].Do();
                    }

                    // Levels keep moving like a real microphone so the meters glide.
                    var wave = (float)(0.5 + 0.4 * Math.Sin(clock.Elapsed.TotalSeconds * 5));
                    form.Home.SystemLevel.Push(wave);
                    form.Home.MicrophoneLevel.Push(wave * 0.4f);
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(10);
                }

                process.WaitForExit(5000);
                Console.WriteLine($"demo video {path}: ffmpeg exit {(process.HasExited ? process.ExitCode : -1)}, {steps.Count} steps");
                return process.HasExited && process.ExitCode == 0 && File.Exists(path) ? 0 : 1;
            }
        }
    }
}

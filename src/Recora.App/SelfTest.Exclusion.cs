using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Recora.App.Audio;
using Recora.App.Logging;
using Recora.App.Native;
using Recora.App.Recording;
using Recora.App.UI;
using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Recording;

namespace Recora.App
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// <c>--selftest-exclusion &lt;dir&gt;</c>: Recora's window, excluded from capture, sits in the middle of a black
        /// screen while Recora records. CI then checks the middle of the video is black: the window never got into it.
        /// </summary>
        public static int Exclusion(string directory)
        {
            Directory.CreateDirectory(directory);
            Log.Initialize(directory, echoToConsole: true);
            var (recorder, environment) = NewRecorder();
            var monitor = environment.MonitorFor(string.Empty);

            using (var board = new SyncBoard())
            using (var form = new MainForm())
            {
                board.Show();
                var screen = Screen.PrimaryScreen.Bounds;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(screen.X + (screen.Width - form.Width) / 2, screen.Y + (screen.Height - form.Height) / 2);
                form.TopMost = true;
                form.Show();
                var excluded = NativeMethods.SetWindowDisplayAffinity(form.Handle, NativeMethods.WdaExcludeFromCapture);
                Log.Info($"exclusion: window {form.Bounds} on screen {screen}, affinity set = {excluded}");
                PumpFor(TimeSpan.FromMilliseconds(500));

                var request = new RecordingRequest(
                    new MonitorSource(monitor.Target), VideoCodec.H264, null, QualityPreset.Balanced, 30, OutputScale.Original,
                    DrawCursor: false, ContainerFormat.Mp4, Path.Combine(directory, "video"), new AudioTrackSpec[0]);

                var run = Task.Run(async () =>
                {
                    await recorder.StartAsync(request).ConfigureAwait(false);
                    Thread.Sleep(1500);
                    return await recorder.StopAsync().ConfigureAwait(false);
                });

                // The windows must keep painting while the recorder works on the thread pool.
                while (!run.IsCompleted)
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }

                var saved = run.GetAwaiter().GetResult();
                var frame = form.Bounds;
                File.WriteAllText(
                    Path.Combine(directory, "exclusion.txt"),
                    $"output={saved?.Path}\nexcluded={excluded}\nwindow={frame.X - screen.X},{frame.Y - screen.Y},{frame.Width},{frame.Height}\nscreen={screen.Width},{screen.Height}\n");
                Console.WriteLine(saved == null ? "exclusion: nothing recorded" : "exclusion: recorded " + saved.Path);
                return saved != null && excluded ? 0 : 1;
            }
        }

        private static void PumpFor(TimeSpan duration)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < duration)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
        }
    }
}

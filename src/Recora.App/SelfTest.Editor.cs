using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Recora.App.Editor;
using Recora.App.Logging;
using Recora.App.UI.Controls;
using Recora.Core.Editor;
using Recora.Core.Recording;

namespace Recora.App
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// <c>--selftest-editor &lt;dir&gt;</c>: records 3 s, gets Clipora (installed, or downloaded and checked against
        /// SHA256SUMS), embeds its window in a test form, puts the recording on a timeline over the control port,
        /// photographs the form, then hands Clipora its own window back and quits it. Writes <c>editor.txt</c>.
        /// </summary>
        public static int Editor(string directory)
        {
            Directory.CreateDirectory(directory);
            Log.Initialize(directory, echoToConsole: true);
            var lines = new StringBuilder();
            void Note(string key, object? value)
            {
                lines.Append(key).Append('=').Append(value).Append('\n');
                Console.WriteLine($"{key}={value}");
            }

            var (recorder, environment) = NewRecorder();
            var video = Task.Run(async () =>
            {
                await recorder.StartAsync(Request(environment, Path.Combine(directory, "videos"))).ConfigureAwait(false);
                if (recorder.State != SessionState.Recording)
                {
                    return null;
                }

                Thread.Sleep(3000);
                return await recorder.StopAsync().ConfigureAwait(false);
            }).GetAwaiter().GetResult();
            Note("video", video?.Path);

            var clock = Stopwatch.StartNew();
            var install = CliporaRuntime.Find() ?? CliporaRuntime.Download((_, _) => { }, CancellationToken.None);
            Note("clipora", install.Exe);
            Note("origin", install.Origin);
            Note("download_seconds", clock.Elapsed.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));

            var ok = video != null;
            using (var form = new Form
            {
                Text = "Recora — Dựng phim (selftest)",
                StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(30, 30, 1500, 950),
                ShowInTaskbar = false,
            })
            {
                var host = new WindowHost { Dock = DockStyle.Fill };
                form.Controls.Add(host);
                form.Show();
                Pump(TimeSpan.FromMilliseconds(500));

                var session = CliporaSession.Start(install.Exe);
                var window = session.WaitForWindow(TimeSpan.FromSeconds(60));
                Note("window", window == IntPtr.Zero ? "none" : "0x" + window.ToInt64().ToString("x"));
                Note("alive", session.IsAlive);
                if (window != IntPtr.Zero)
                {
                    host.Adopt(window);
                    Pump(TimeSpan.FromSeconds(2));
                    Note("embedded", host.Holds(window));
                    ok &= host.Holds(window);

                    var added = video == null ? 0 : session.AddToTimeline(new[] { video.Path });
                    Note("added", added);
                    ok &= added > 0;

                    var project = session.Execute("project.inspect", "{}");
                    File.WriteAllText(Path.Combine(directory, "editor-project.json"), project.Raw);
                    Note("project_ok", project.Ok);

                    var own = Path.Combine(directory, "editor-clipora.png");
                    var shot = session.Send($"{{\"id\":99,\"method\":\"ui.screenshot\",\"params\":{{\"path\":{CliporaControl.Json(own)}}}}}");
                    Note("clipora_screenshot", shot.Ok ? own : "failed: " + shot.Error);

                    // Long enough for the guest to re-apply its own frame once; the host's watch must undo it.
                    Pump(TimeSpan.FromSeconds(4));
                    Note("fits", host.Fits);
                    ok &= host.Fits;
                    var picture = Path.Combine(directory, "editor-embedded.png");
                    var area = form.RectangleToScreen(form.ClientRectangle);
                    using (var bitmap = new Bitmap(area.Width, area.Height))
                    {
                        using (var g = Graphics.FromImage(bitmap))
                        {
                            g.CopyFromScreen(area.Location, Point.Empty, area.Size);
                        }

                        bitmap.Save(picture, ImageFormat.Png);
                    }

                    Note("embedded_screenshot", picture);
                    host.Release();
                    Pump(TimeSpan.FromMilliseconds(500));
                    Note("released", !host.Holds(window));
                }
                else
                {
                    ok = false;
                }

                session.Call("app.quit");
                if (!session.Process.WaitForExit(8000))
                {
                    session.Process.Kill();
                }

                Note("quit_exit_code", session.Process.HasExited ? session.Process.ExitCode.ToString() : "killed");
            }

            File.WriteAllText(Path.Combine(directory, "editor.txt"), lines.ToString());
            return ok ? 0 : 1;
        }
    }
}

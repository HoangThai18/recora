using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Recora.App.Logging;
using Recora.App.Recording;
using Recora.Core.Recording;
using static System.FormattableString;

namespace Recora.App
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// <c>--selftest-trim &lt;dir&gt;</c>: records 6 s, reads its length the way the cut dialog does for foreign files,
        /// grabs a preview still, and cuts 2.0-4.5 s. Writes <c>trim.txt</c> for CI to check with ffprobe.
        /// </summary>
        public static int Trim(string directory)
        {
            Directory.CreateDirectory(directory);
            Log.Initialize(directory, echoToConsole: true);
            var (recorder, environment) = NewRecorder();
            var ffmpeg = environment.FfmpegPath;

            var source = Task.Run(async () =>
            {
                await recorder.StartAsync(Request(environment, Path.Combine(directory, "videos"))).ConfigureAwait(false);
                if (recorder.State != SessionState.Recording)
                {
                    return null;
                }

                Thread.Sleep(6000);
                return await recorder.StopAsync().ConfigureAwait(false);
            }).GetAwaiter().GetResult();

            if (source == null)
            {
                Console.WriteLine("trim self-test: the source recording failed");
                return 1;
            }

            var from = TimeSpan.FromSeconds(2.0);
            var to = TimeSpan.FromSeconds(4.5);
            var probed = TrimService.Duration(ffmpeg, source.Path);
            bool previewOk;
            using (var frame = TrimService.Frame(ffmpeg, source.Path, TimeSpan.FromSeconds(3)))
            {
                previewOk = frame != null && frame.Width > 0;
            }

            var output = TrimService.Cut(ffmpeg, source.Path, from, to);
            var lines = new StringBuilder()
                .Append(Invariant($"source={source.Path}\nsource_seconds={probed?.TotalSeconds ?? 0:0.000}\n"))
                .Append(Invariant($"recorded_seconds={source.Duration.TotalSeconds:0.000}\npreview_ok={previewOk}\n"))
                .Append(Invariant($"output={output}\nfrom={from.TotalSeconds:0.000}\nto={to.TotalSeconds:0.000}\n"));
            File.WriteAllText(Path.Combine(directory, "trim.txt"), lines.ToString());
            Console.WriteLine(lines.ToString());
            return probed.HasValue && previewOk ? 0 : 1;
        }
    }
}

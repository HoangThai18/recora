using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Recora.App.Audio;
using Recora.App.Logging;
using Recora.App.Recording;
using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;
using Recora.Core.Settings;

namespace Recora.App
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// <c>--selftest-modes &lt;dir&gt;</c>: a 640×360 region, sound-only recordings in M4A and WAV, and an F11
        /// screenshot, all through the code the app uses. Writes <c>modes.txt</c> (key=path) for CI to check with ffprobe.
        /// </summary>
        public static int Modes(string directory)
        {
            Directory.CreateDirectory(directory);
            Log.Initialize(directory, echoToConsole: true);
            var (recorder, environment) = NewRecorder();
            var monitor = environment.MonitorFor(string.Empty).Target;
            var lines = new StringBuilder();
            var ok = true;

            SavedRecording? Record(RecordingRequest request, int milliseconds) => Task.Run(async () =>
            {
                await recorder.StartAsync(request).ConfigureAwait(false);
                if (recorder.State != SessionState.Recording)
                {
                    return null;
                }

                Thread.Sleep(milliseconds);
                return await recorder.StopAsync().ConfigureAwait(false);
            }).GetAwaiter().GetResult();

            RecordingRequest Request(CaptureSource source, string folder, AudioFormat? audioOnly) => new RecordingRequest(
                source, VideoCodec.H264, null, QualityPreset.Balanced, 30, OutputScale.Original, DrawCursor: true, ContainerFormat.Mp4,
                Path.Combine(directory, folder),
                new[] { new AudioTrackSpec("Âm thanh", new (IAudioInput, float)[] { (new ToneInput(440, 0.2f), 1f) }) },
                audioOnly);

            var region = new PixelRect(monitor.Bounds.X + 100, monitor.Bounds.Y + 80, 640, 360);
            var video = Record(Request(new RegionSource(monitor, region), "region", null), 2000);
            lines.Append($"region={video?.Path}\n");
            ok &= video != null;

            foreach (var format in new[] { AudioFormat.M4a, AudioFormat.Wav })
            {
                var audio = Record(Request(new MonitorSource(monitor), "audio", format), 2000);
                lines.Append($"audio_{format.ToString().ToLowerInvariant()}={audio?.Path}\n");
                ok &= audio != null;
            }

            var bounds = monitor.Bounds;
            var shot = ScreenshotService.Take(new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height), Path.Combine(directory, "shots"), ScreenshotFormat.Png, copyToClipboard: false, sound: false);
            lines.Append($"screenshot={shot.Path}\nscreen={bounds.Width}x{bounds.Height}\n");

            File.WriteAllText(Path.Combine(directory, "modes.txt"), lines.ToString());
            Console.WriteLine(lines.ToString());
            return ok ? 0 : 1;
        }
    }
}

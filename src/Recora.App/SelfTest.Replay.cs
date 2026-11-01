using System;
using System.IO;
using System.Text;
using System.Threading;
using Recora.App.Audio;
using Recora.App.Logging;
using Recora.App.Recording;
using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;
using Recora.Core.Settings;
using static System.FormattableString;

namespace Recora.App
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// <c>--selftest-replay &lt;dir&gt;</c>: keeps the last 6 s for longer than the ring holds (so chunks get reused),
        /// saves twice while it keeps running, then stops. Writes <c>replay.txt</c> for CI to check with ffprobe.
        /// </summary>
        public static int Replay(string directory)
        {
            const int seconds = 6;
            Directory.CreateDirectory(directory);
            Log.Initialize(directory, echoToConsole: true);
            var environment = CaptureEnvironment.Detect();
            var buffer = new ReplayBuffer(environment, new EncoderMemory(new AppSettings(), environment.Fingerprint));
            var output = Path.Combine(directory, "videos");
            var monitor = environment.MonitorFor(string.Empty).Target;
            var request = new RecordingRequest(
                new MonitorSource(monitor), VideoCodec.H264, null, QualityPreset.Balanced, 30, OutputScale.Original, DrawCursor: true, ContainerFormat.Mp4,
                output, new[] { new AudioTrackSpec("Âm thanh", new (IAudioInput, float)[] { (new ToneInput(440, 0.2f), 1f) }) });
            var lines = new StringBuilder();

            buffer.Start(request, seconds);
            var ring = ReplayCommand.RingSize(seconds);
            Thread.Sleep(TimeSpan.FromSeconds(ring * ReplayCommand.ChunkSeconds + 4));

            var chunks = Directory.GetFiles(ReplayBuffer.FolderFor(output), "chunk*.mkv").Length;
            var first = buffer.Save(output, ContainerFormat.Mp4);
            Thread.Sleep(3000);
            var second = buffer.Save(output, ContainerFormat.Mp4);
            var runningAfterSaves = buffer.IsRunning;
            buffer.Stop();

            lines.Append(Invariant($"seconds={seconds}\nring={ring}\nchunks={chunks}\n"));
            lines.Append(Invariant($"first={first?.Path}\nfirst_seconds={first?.Duration.TotalSeconds ?? 0:0.000}\n"));
            lines.Append(Invariant($"second={second?.Path}\nrunning_after_saves={runningAfterSaves}\n"));
            lines.Append(Invariant($"folder_removed={!Directory.Exists(ReplayBuffer.FolderFor(output))}\nencoder={buffer.EncoderName}\n"));
            File.WriteAllText(Path.Combine(directory, "replay.txt"), lines.ToString());
            Console.WriteLine(lines.ToString());
            return first != null && second != null && runningAfterSaves ? 0 : 1;
        }
    }
}

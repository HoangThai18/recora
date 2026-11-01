using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;
using Recora.Core.Tests.Capture;

namespace Recora.Core.Tests.Ffmpeg
{
    /// <summary>
    /// Recora ships a minimal FFmpeg: a filter or muxer missing from tools/ffmpeg/components.sh fails only on a user's
    /// machine (the webcam overlay's hflip did until 0.2.0). Every command the app builds is checked against the list.
    /// </summary>
    public class FfmpegBuildCoverageTests
    {
        private static readonly Lazy<string> Components = new Lazy<string>(() => File.ReadAllText(FindComponents()));

        public static IEnumerable<object[]> Commands()
        {
            var monitor = new MonitorSource(Monitors.Primary());
            var region = new RegionSource(Monitors.Primary(), new PixelRect(100, 100, 640, 360));
            var webcam = new DeviceSource("Cam", new PixelRect(0, 0, 1280, 720), Monitors.Primary());
            var pipes = new[] { "p" };

            foreach (CaptureBackend backend in Enum.GetValues(typeof(CaptureBackend)))
            {
                foreach (EncoderKind kind in Enum.GetValues(typeof(EncoderKind)))
                {
                    foreach (var scale in new[] { OutputScale.Original, OutputScale.P720 })
                    {
                        CaptureSource source = backend == CaptureBackend.Dshow ? webcam : backend == CaptureBackend.Gdigrab ? region : monitor;
                        var plan = new RecordingPlan(source, backend, new VideoEncoder(kind, VideoCodec.H264), QualityPreset.Balanced, 60, scale, true, AudioLayout.Mixed);
                        yield return new object[] { $"record {backend}/{kind}/{scale}", RecordingCommand.Build(plan, "a.mkv", pipes) };
                        yield return new object[] { $"replay {backend}/{kind}/{scale}", ReplayCommand.Build(plan, "f", pipes, 30) };
                    }
                }
            }

            yield return new object[] { "webcam preview", WebcamPreviewCommand.Build("Cam", mirror: true) };
            yield return new object[] { "webcam preview plain", WebcamPreviewCommand.Build("Cam", mirror: false) };
            yield return new object[] { "frame preview", FramePreviewCommand.Build("a.mp4", TimeSpan.FromSeconds(1)) };
            yield return new object[] { "finalize mp4", FinalizeCommand.Build("l.txt", "a.mp4", ContainerFormat.Mp4, VideoCodec.Hevc) };
            yield return new object[] { "finalize mkv", FinalizeCommand.Build("l.txt", "a.mkv", ContainerFormat.Mkv, VideoCodec.H264) };
            foreach (AudioFormat format in Enum.GetValues(typeof(AudioFormat)))
            {
                yield return new object[] { "audio " + format, AudioRecordingCommand.Build("p", format, QualityPreset.Balanced, "a.mka") };
                yield return new object[] { "finalize audio " + format, FinalizeCommand.BuildAudio("l.txt", "a" + AudioRecordingCommand.Extension(format), format) };
            }

            foreach (var file in new[] { "a.mp4", "a.mkv", "a.m4a", "a.mp3", "a.wav" })
            {
                yield return new object[] { "trim " + file, TrimCommand.Build(file, file, TimeSpan.Zero, TimeSpan.FromSeconds(1)) };
            }
        }

        [Theory]
        [MemberData(nameof(Commands))]
        public void EveryFilterAndOutputFormat_IsInTheFfmpegBuild(string name, FfmpegArgs command)
        {
            var filters = Names("FILTERS");
            var muxers = Names("MUXERS");
            var demuxers = new HashSet<string>(Names("DEMUXERS").Concat(Names("INDEVS")));
            var items = command.Items;

            for (var i = 0; i < items.Count - 1; i++)
            {
                if (items[i] == "-vf" || items[i] == "-af")
                {
                    foreach (var filter in FilterNames(items[i + 1]))
                    {
                        Assert.True(filters.Contains(filter), $"{name}: filter '{filter}' is not built (tools/ffmpeg/components.sh)");
                    }
                }

                if (items[i] == "-f")
                {
                    var isInput = IsInputFormat(items, i);
                    var format = items[i + 1] == "f32le" ? "pcm_f32le" : items[i + 1];
                    var known = isInput ? demuxers : muxers;
                    Assert.True(known.Contains(format), $"{name}: {(isInput ? "demuxer" : "muxer")} '{format}' is not built");
                }

                if (items[i] == "-segment_format")
                {
                    Assert.Contains(items[i + 1], muxers);
                }
            }
        }

        [Fact]
        public void LavfiSources_AreBuilt()
        {
            var filters = Names("FILTERS");
            Assert.Contains("ddagrab", filters);
            Assert.Contains("gfxcapture", filters);
        }

        /// <summary>An input's -f is followed by its -i before any output option shows up.</summary>
        private static bool IsInputFormat(IReadOnlyList<string> items, int at)
        {
            var outputOptions = new[] { "-map", "-c", "-c:v", "-c:a", "-vf", "-af", "-frames:v", "-an" };
            for (var j = at + 2; j < items.Count; j++)
            {
                if (items[j] == "-i")
                {
                    return true;
                }

                if (outputOptions.Contains(items[j]))
                {
                    return false;
                }
            }

            return false;
        }

        private static IEnumerable<string> FilterNames(string chain) =>
            Regex.Split(chain, @"(?<!\\)[,;]").Select(f => f.Split('=')[0].Trim()).Where(f => f.Length > 0);

        private static HashSet<string> Names(string variable)
        {
            var match = Regex.Match(Components.Value, "(?m)^" + variable + "=\"([^\"]*)\"");
            Assert.True(match.Success, variable + " not found in components.sh");
            return new HashSet<string>(match.Groups[1].Value.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string FindComponents()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "tools", "ffmpeg", "components.sh");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException("tools/ffmpeg/components.sh not found above " + AppContext.BaseDirectory);
        }
    }
}

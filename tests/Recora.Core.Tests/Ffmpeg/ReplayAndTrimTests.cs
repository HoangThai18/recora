using System;
using System.IO;
using System.Linq;
using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;
using Recora.Core.Tests.Capture;

namespace Recora.Core.Tests.Ffmpeg
{
    public class ReplayCommandTests
    {
        private static readonly string Folder = Path.Combine("D", "Videos", ".recora", "replay");

        private static RecordingPlan Plan(AudioLayout audio = AudioLayout.Mixed) =>
            new RecordingPlan(new MonitorSource(Monitors.Primary()), CaptureBackend.Ddagrab, new VideoEncoder(EncoderKind.Nvenc, VideoCodec.Hevc),
                QualityPreset.Balanced, 60, OutputScale.Original, DrawCursor: true, audio, MaxGigabytesPerHour: 2);

        [Theory]
        [InlineData(15, 13)]
        [InlineData(30, 20)]
        [InlineData(60, 35)]
        [InlineData(300, 155)]
        [InlineData(0, 6)]
        public void RingSize_CoversTheLengthPlusAMargin(int seconds, int expected) =>
            Assert.Equal(expected, ReplayCommand.RingSize(seconds));

        [Fact]
        public void Build_EncodesLikeARecording_ButWritesARingOfChunks()
        {
            var plan = Plan();
            var args = ReplayCommand.Build(plan, Folder, new[] { @"\\.\pipe\a" }, 30);
            var recording = RecordingCommand.Build(plan, "x.mkv", new[] { @"\\.\pipe\a" });

            Assert.Equal("segment", args.Items[args.Items.ToList().LastIndexOf("-f") + 1]);
            Assert.Equal("matroska", args.ValueOf("-segment_format"));
            Assert.Equal("2", args.ValueOf("-segment_time"));
            Assert.Equal("20", args.ValueOf("-segment_wrap"));
            Assert.Equal("20", args.ValueOf("-segment_list_size"));
            Assert.Equal("csv", args.ValueOf("-segment_list_type"));
            Assert.Equal("1", args.ValueOf("-reset_timestamps"));
            Assert.Equal(Path.Combine(Folder, "replay.csv"), args.ValueOf("-segment_list"));
            Assert.Equal(Path.Combine(Folder, "chunk%03d.mkv"), args.Items.Last());

            // Same capture, encoder, size limit and sound as F12 would use.
            Assert.Equal(recording.ValueOf("-c:v"), args.ValueOf("-c:v"));
            Assert.Equal(recording.ValueOf("-maxrate"), args.ValueOf("-maxrate"));
            Assert.Equal("120", args.ValueOf("-g"));
            Assert.Equal("aac", args.ValueOf("-c:a"));
            Assert.Equal(recording.ValueOf("-i"), args.ValueOf("-i"));
        }

        [Fact]
        public void Build_ChecksTheAudioPipesLikeARecording() =>
            Assert.Throws<ArgumentException>(() => ReplayCommand.Build(Plan(AudioLayout.SeparateTracks), Folder, new[] { "one" }, 30));
    }

    public class TrimCommandTests
    {
        private static readonly string Video = Path.Combine("D", "Videos", "Recora 2026-11-01 20-15-03.mp4");

        [Fact]
        public void Build_SeeksOnTheInputAndCopiesEveryStream()
        {
            var output = Path.Combine("D", "Videos", "out.mp4");
            var args = TrimCommand.Build(Video, output, TimeSpan.FromSeconds(12.5), TimeSpan.FromMinutes(1.25)).Items.ToList();

            var input = args.IndexOf("-i");
            Assert.True(args.IndexOf("-ss") < input && args.IndexOf("-to") < input);
            Assert.Equal("12.500", args[args.IndexOf("-ss") + 1]);
            Assert.Equal("75.000", args[args.IndexOf("-to") + 1]);
            Assert.Equal("copy", args[args.IndexOf("-c") + 1]);
            Assert.Equal("0", args[args.IndexOf("-map") + 1]);
            Assert.Equal("make_zero", args[args.IndexOf("-avoid_negative_ts") + 1]);
            Assert.Equal("mp4", args[args.IndexOf("-f") + 1]);
            Assert.Equal(output, args.Last());
        }

        [Theory]
        [InlineData("a.mkv", "matroska")]
        [InlineData("a.M4A", "ipod")]
        [InlineData("a.mp3", "mp3")]
        [InlineData("a.wav", "wav")]
        [InlineData("a.png", null)]
        [InlineData("a.avi", null)]
        public void Muxer_FollowsTheExtension(string file, string? expected) => Assert.Equal(expected, TrimCommand.Muxer(file));

        [Fact]
        public void Build_RejectsAnEmptyRange() =>
            Assert.Throws<ArgumentException>(() => TrimCommand.Build(Video, "b.mp4", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));

        [Fact]
        public void OutputPath_SitsNextToTheSourceAndIsNumberedWhenTaken()
        {
            var first = Path.Combine("D", "Videos", "Recora 2026-11-01 20-15-03 (cắt).mp4");

            Assert.Equal(first, TrimCommand.OutputPath(Video, _ => false));
            Assert.NotEqual(first, TrimCommand.OutputPath(Video, path => path == first));
            Assert.EndsWith(".mp4", TrimCommand.OutputPath(Video, path => path == first));
        }

        [Theory]
        [InlineData("  Duration: 00:01:02.50, start: 0.000000, bitrate: 2400 kb/s", 62.5)]
        [InlineData("Input #0, matroska,webm\n  Duration: 01:00:00.04, start: 0", 3600.04)]
        [InlineData("  Duration: N/A, start: 1793464516.479924", null)]
        [InlineData("garbage", null)]
        public void MediaInfo_ParsesTheDurationLine(string stderr, double? expected) =>
            Assert.Equal(expected, MediaInfoCommand.ParseDuration(stderr)?.TotalSeconds);

        [Fact]
        public void FramePreview_OneBgraFrameInAFixedBox()
        {
            var args = FramePreviewCommand.Build(Video, TimeSpan.FromSeconds(-3));

            Assert.Equal("0.000", args.ValueOf("-ss"));
            Assert.Equal("1", args.ValueOf("-frames:v"));
            Assert.Equal("bgra", args.ValueOf("-pix_fmt"));
            Assert.Contains("pad=480:270", args.ValueOf("-vf"));
            Assert.Equal(480 * 270 * 4, FramePreviewCommand.FrameBytes);
        }
    }
}

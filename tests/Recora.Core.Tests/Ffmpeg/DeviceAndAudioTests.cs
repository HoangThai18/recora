using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;
using Recora.Core.Tests.Capture;

namespace Recora.Core.Tests.Ffmpeg
{
    public class DeviceRecordingTests
    {
        private static DeviceSource Webcam() => new DeviceSource("HD Pro Webcam C920", new PixelRect(0, 0, 1280, 720), Monitors.Primary());

        private static RecordingPlan Plan(EncoderKind kind = EncoderKind.Nvenc, OutputScale scale = OutputScale.Original) =>
            new RecordingPlan(Webcam(), CaptureBackend.Dshow, new VideoEncoder(kind, VideoCodec.H264), QualityPreset.Balanced, 60, scale, DrawCursor: false, AudioLayout.Mixed);

        [Fact]
        public void Selector_PicksDshowForDevices_EvenWhenGpuCaptureFailed()
        {
            Assert.Equal(CaptureBackend.Dshow, CaptureBackendSelector.Select(Webcam(), new CaptureCapabilities(true, true)));
            Assert.Equal(CaptureBackend.Dshow, CaptureBackendSelector.Select(Webcam(), CaptureCapabilities.CpuOnly));
        }

        [Fact]
        public void Input_OpensTheDeviceByNameWithItsOwnSizeAndRate()
        {
            var args = RecordingCommand.VideoInput(Plan()).ToList();

            Assert.Equal("dshow", args[args.IndexOf("-f") + 1]);
            Assert.Equal("video=HD Pro Webcam C920", args[args.IndexOf("-i") + 1]);
            Assert.DoesNotContain("-video_size", args);
            Assert.DoesNotContain("-framerate", args);
        }

        [Theory]
        [InlineData(EncoderKind.Nvenc, "nv12")]
        [InlineData(EncoderKind.Qsv, "nv12")]
        [InlineData(EncoderKind.X264, "yuv420p")]
        public void Filter_ConvertsOnCpu_NeverTouchesD3d11(EncoderKind kind, string format)
        {
            var chain = RecordingCommand.VideoFilterChain(Plan(kind));

            Assert.Equal($"scale=-2:trunc(ih/2)*2,format={format}", chain);
            Assert.DoesNotContain("d3d11", chain);
            Assert.DoesNotContain("hwdownload", chain);
        }

        [Fact]
        public void Filter_CapsHeight_WithTheCommaEscapedForTheFilterGraph()
        {
            Assert.Equal(@"scale=-2:trunc(min(ih\,720)/2)*2,format=nv12", RecordingCommand.VideoFilterChain(Plan(scale: OutputScale.P720)));
        }

        [Fact]
        public void Output_KeepsTheDeviceRateAndForcesNoColorTags()
        {
            var output = RecordingCommand.VideoOutput(Plan()).ToList();

            Assert.Equal("cfr", output[output.IndexOf("-fps_mode") + 1]);
            Assert.DoesNotContain("-r", output);
            Assert.DoesNotContain("-colorspace", output);
        }
    }

    public class DshowDevicesTests
    {
        private const string Ffmpeg8Listing = """
            [in#0 @ 000001d2a0b6f2c0] "HD Pro Webcam C920" (video)
            [in#0 @ 000001d2a0b6f2c0]   Alternative name "@device_pnp_\\?\usb#vid_046d&pid_082d&mi_00#7&1b2b3c&0&0000#{65e8773d}\global"
            [in#0 @ 000001d2a0b6f2c0] "OBS Virtual Camera" (video)
            [in#0 @ 000001d2a0b6f2c0] "Microphone (HD Pro Webcam C920)" (audio)
            [in#0 @ 000001d2a0b6f2c0]   Alternative name "@device_cm_{33D9A762}\wave_{0A1B}"
            [in#0 @ 000001d2a0b6f2c0] "Elgato Game Capture HD60 S" (video, audio)
            Error opening input file dummy.
            """;

        [Fact]
        public void ParseVideo_ListsVideoDevicesOnly_InOrder()
        {
            Assert.Equal(
                new[] { "HD Pro Webcam C920", "OBS Virtual Camera", "Elgato Game Capture HD60 S" },
                DshowDevices.ParseVideo(Ffmpeg8Listing));
        }

        [Fact]
        public void ParseVideo_NoDevices_GivesEmptyList() =>
            Assert.Empty(DshowDevices.ParseVideo("[in#0 @ 0000] Could not enumerate video devices (or none found).\nError opening input file dummy."));

        [Fact]
        public void ListCommand_AsksDirectShowForItsDevices()
        {
            var args = DshowDevices.ListCommand().Items.ToList();

            Assert.Equal("true", args[args.IndexOf("-list_devices") + 1]);
            Assert.Equal("dshow", args[args.IndexOf("-f") + 1]);
        }
    }

    public class AudioRecordingTests
    {
        private const string Pipe = @"\\.\pipe\recora-audio";
        private const string Segment = @"D:\Rec\.recora\s1\001.mkv";

        [Theory]
        [InlineData(AudioFormat.M4a, "aac", ".m4a", "ipod")]
        [InlineData(AudioFormat.Mp3, "mp3_mf", ".mp3", "mp3")]
        [InlineData(AudioFormat.Wav, "pcm_s16le", ".wav", "wav")]
        public void Formats_PickCodecExtensionAndMuxer(AudioFormat format, string codec, string extension, string muxer)
        {
            var args = AudioRecordingCommand.Build(Pipe, format, QualityPreset.Balanced, Segment).Items.ToList();

            Assert.Equal(codec, args[args.IndexOf("-c:a") + 1]);
            Assert.Equal(extension, AudioRecordingCommand.Extension(format));
            Assert.Equal(muxer, AudioRecordingCommand.Muxer(format));
        }

        [Fact]
        public void Build_ReadsThePipeOnly_AndWritesACrashSafeSegment()
        {
            var args = AudioRecordingCommand.Build(Pipe, AudioFormat.M4a, QualityPreset.Balanced, Segment).Items.ToList();

            Assert.Single(args, a => a == "-i");
            Assert.Equal(Pipe, args[args.IndexOf("-i") + 1]);
            Assert.DoesNotContain("-c:v", args);
            Assert.Equal("1", args[args.IndexOf("-flush_packets") + 1]);
            Assert.Equal("matroska", args[args.LastIndexOf("-f") + 1]);
            Assert.Equal(Segment, args[^1]);
        }

        [Fact]
        public void Bitrate_IsHigherThanTheVideoTrackButNeverBelow160k()
        {
            var small = AudioRecordingCommand.Codec(AudioFormat.M4a, QualityPreset.Small).ToList();
            var high = AudioRecordingCommand.Codec(AudioFormat.M4a, QualityPreset.High).ToList();

            Assert.Equal("160k", small[small.IndexOf("-b:a") + 1]);
            Assert.Equal("224k", high[high.IndexOf("-b:a") + 1]);
        }

        [Fact]
        public void FinalizeAudio_CopiesTheSoundIntoTheChosenFile()
        {
            var args = FinalizeCommand.BuildAudio(@"D:\Rec\.recora\s1\concat.txt", @"D:\Rec\Recora 2026-10-31 20-00-00.m4a", AudioFormat.M4a).Items.ToList();

            Assert.Equal("copy", args[args.IndexOf("-c") + 1]);
            Assert.Equal("0:a", args[args.IndexOf("-map") + 1]);
            Assert.Equal("ipod", args[args.LastIndexOf("-f") + 1]);
        }
    }
}

namespace Recora.Core.Tests.Ffmpeg
{
    public class WebcamPreviewTests
    {
        [Fact]
        public void WebcamPreview_WritesFixedSizeBgraFramesToStdout()
        {
            var args = WebcamPreviewCommand.Build("HD Pro Webcam C920", mirror: true).Items.ToList();

            Assert.Equal("video=HD Pro Webcam C920", args[args.IndexOf("-i") + 1]);
            Assert.StartsWith("hflip,scale=320:240", args[args.IndexOf("-vf") + 1]);
            Assert.Equal("bgra", args[args.IndexOf("-pix_fmt") + 1]);
            Assert.Equal("pipe:1", args[^1]);
            Assert.Equal(320 * 240 * 4, WebcamPreviewCommand.FrameBytes);
        }
    }
}

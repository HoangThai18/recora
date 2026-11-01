using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;
using Recora.Core.Tests.Capture;

namespace Recora.Core.Tests.Ffmpeg
{
    public class RecordingCommandTests
    {
        private const string Segment = @"D:\Videos\Recora\.recora\s1\001.mkv";
        private const string Pipe1 = @"\\.\pipe\recora-1-1-a";
        private const string Pipe2 = @"\\.\pipe\recora-1-1-b";

        private static RecordingPlan Plan(
            CaptureSource? source = null,
            CaptureBackend backend = CaptureBackend.Ddagrab,
            EncoderKind kind = EncoderKind.Nvenc,
            VideoCodec codec = VideoCodec.H264,
            OutputScale scale = OutputScale.Original,
            AudioLayout audio = AudioLayout.Mixed) =>
            new RecordingPlan(
                source ?? new MonitorSource(Monitors.Primary()),
                backend,
                new VideoEncoder(kind, codec),
                QualityPreset.Balanced,
                Fps: 60,
                scale,
                DrawCursor: true,
                audio);

        private static IReadOnlyList<string> Pipes(RecordingPlan plan) => plan.AudioTrackCount switch
        {
            0 => new string[0],
            1 => new[] { Pipe1 },
            _ => new[] { Pipe1, Pipe2 },
        };

        private static List<string> Build(RecordingPlan plan) =>
            RecordingCommand.Build(plan, Segment, Pipes(plan)).Items.ToList();

        [Fact]
        public void RecordingCommand_AudioPipe_ComesAfterVideoInput()
        {
            var args = Build(Plan(audio: AudioLayout.SeparateTracks));

            var inputs = args.Select((a, i) => (a, i)).Where(p => p.a == "-i").Select(p => args[p.i + 1]).ToList();
            Assert.StartsWith("ddagrab=", inputs[0]);
            Assert.Equal(new[] { Pipe1, Pipe2 }, inputs.Skip(1));
            Assert.Equal("f32le", args[args.IndexOf(Pipe1) - 6]);
            Assert.Contains("1:a:0", args);
            Assert.Contains("2:a:0", args);
            Assert.Contains("title=Tiếng máy", args);
        }

        [Theory]
        [InlineData(CaptureBackend.Ddagrab)]
        [InlineData(CaptureBackend.Gfxcapture)]
        [InlineData(CaptureBackend.Gdigrab)]
        public void VideoInput_ProbesOneFrameOnly_SoTheAudioPipeOpensRightAfterTheFirstFrame(CaptureBackend backend)
        {
            var input = RecordingCommand.VideoInput(Plan(backend: backend)).ToList();

            Assert.Equal("32", ValueAfter(input, "-probesize"));
            Assert.Equal("0", ValueAfter(input, "-analyzeduration"));
            Assert.True(input.IndexOf("-probesize") < input.IndexOf("-i"));
        }

        public static IEnumerable<object[]> EveryEncoderCodecAndSource()
        {
            var monitor = Monitors.Primary();
            var sources = new CaptureSource[]
            {
                new MonitorSource(monitor),
                new RegionSource(monitor, new PixelRect(monitor.Bounds.X + 10, monitor.Bounds.Y + 10, 640, 360)),
                new WindowSource(0x1234, "Notepad", new PixelRect(monitor.Bounds.X, monitor.Bounds.Y, 800, 600), monitor),
            };

            foreach (EncoderKind kind in Enum.GetValues(typeof(EncoderKind)))
            {
                foreach (VideoCodec codec in Enum.GetValues(typeof(VideoCodec)))
                {
                    if (EncoderCatalog.FfmpegName(kind, codec) == null)
                    {
                        continue;
                    }

                    foreach (var backend in new[] { CaptureBackend.Ddagrab, CaptureBackend.Gfxcapture, CaptureBackend.Gdigrab })
                    {
                        foreach (var source in sources)
                        {
                            yield return new object[] { kind, codec, backend, source };
                        }
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(EveryEncoderCodecAndSource))]
        public void Build_EveryEncoderCodecBackendAndSource_GivesOneVideoInputAndTheRightEncoder(EncoderKind kind, VideoCodec codec, CaptureBackend backend, CaptureSource source)
        {
            var args = Build(Plan(source, backend, kind, codec));

            Assert.Equal(EncoderCatalog.FfmpegName(kind, codec), ValueAfter(args, "-c:v"));
            Assert.Equal(2, args.Count(a => a == "-i"));
            Assert.Equal("0:v:0", ValueAfter(args, "-map"));
            Assert.Equal(Segment, args[args.Count - 1]);
        }

        [Fact]
        public void Build_WritesProgressToStdoutAndMatroskaSegment()
        {
            var args = Build(Plan());

            Assert.Equal("pipe:1", ValueAfter(args, "-progress"));
            Assert.Equal("matroska", ValueAfter(args, "-f", last: true));
            Assert.Equal(Segment, args[args.Count - 1]);
            Assert.Equal("aac", ValueAfter(args, "-c:a"));
        }

        [Fact]
        public void Build_SegmentSurvivesACrash_PacketsFlushedAndClustersShort()
        {
            var args = Build(Plan());

            Assert.Equal("1", ValueAfter(args, "-flush_packets"));
            Assert.Equal("2000", ValueAfter(args, "-cluster_time_limit"));
        }

        [Fact]
        public void Build_WithoutAudio_HasNoAudioInputOrCodec()
        {
            var args = Build(Plan(audio: AudioLayout.None));

            Assert.DoesNotContain("f32le", args);
            Assert.DoesNotContain("-c:a", args);
            Assert.Single(args, a => a == "-i");
        }

        [Fact]
        public void Build_WhenPipeCountMismatchesLayout_Throws()
        {
            Assert.Throws<ArgumentException>(() => RecordingCommand.Build(Plan(audio: AudioLayout.SeparateTracks), Segment, new[] { Pipe1 }));
        }

        [Theory]
        [InlineData(EncoderKind.Nvenc)]
        [InlineData(EncoderKind.Amf)]
        public void FilterChain_NvencAndAmf_KeepFramesOnTheGpu(EncoderKind kind)
        {
            var args = Build(Plan(kind: kind));

            Assert.DoesNotContain("-vf", args);
            Assert.DoesNotContain(args, a => a.Contains("hwdownload"));
            Assert.DoesNotContain("-colorspace", args);
        }

        [Fact]
        public void FilterChain_AmfDownloadingFrames_ConvertsToNv12WithBt709Tags()
        {
            var args = Build(Plan(kind: EncoderKind.Amf) with { DownloadFrames = true });

            Assert.Equal("hwdownload,format=bgra,scale=out_color_matrix=bt709:out_range=tv,format=nv12", ValueAfter(args, "-vf"));
            Assert.Equal("bt709", ValueAfter(args, "-colorspace"));
            Assert.Equal("h264_amf", ValueAfter(args, "-c:v"));
        }

        [Fact]
        public void FilterChain_AmfDownloadingScaledFrames_ScalesOnTheCpu()
        {
            var args = Build(Plan(kind: EncoderKind.Amf, scale: OutputScale.P720) with { DownloadFrames = true });

            Assert.Contains("scale=1280:720:flags=fast_bilinear:", ValueAfter(args, "-vf"));
            Assert.DoesNotContain("scale_d3d11", ValueAfter(args, "-vf"));
        }

        [Fact]
        public void FilterChain_Qsv_ConvertsToNv12OnGpuAndMapsToQsv()
        {
            var args = Build(Plan(kind: EncoderKind.Qsv));

            Assert.Equal("scale_d3d11=format=nv12,hwmap=derive_device=qsv,format=qsv", ValueAfter(args, "-vf"));
            Assert.Equal("bt470bg", ValueAfter(args, "-colorspace"));
        }

        [Theory]
        [InlineData(EncoderKind.X264, "yuv420p")]
        [InlineData(EncoderKind.MediaFoundation, "nv12")]
        public void FilterChain_CpuEncoders_DownloadAndConvertWithBt709(EncoderKind kind, string format)
        {
            var args = Build(Plan(kind: kind));

            Assert.Equal($"hwdownload,format=bgra,scale=out_color_matrix=bt709:out_range=tv,format={format}", ValueAfter(args, "-vf"));
            Assert.Equal("bt709", ValueAfter(args, "-colorspace"));
        }

        [Fact]
        public void FilterChain_Gdigrab_ConvertsOnCpuEvenForHardwareEncoders()
        {
            var args = Build(Plan(backend: CaptureBackend.Gdigrab, kind: EncoderKind.Nvenc));

            Assert.Equal("scale=out_color_matrix=bt709:out_range=tv,format=nv12", ValueAfter(args, "-vf"));
            Assert.Equal("gdigrab", ValueAfter(args, "-f"));
            Assert.Equal("desktop", ValueAfter(args, "-i"));
            Assert.Equal("1920x1080", ValueAfter(args, "-video_size"));
        }

        [Fact]
        public void Downscale_OnGpu_UsesScaleD3d11WithEvenWidth()
        {
            var source = new MonitorSource(Monitors.Second());

            var plan = Plan(source: source, scale: OutputScale.P1080);
            var args = Build(plan);

            Assert.Equal((1920, 1080), plan.OutputSize);
            Assert.Equal("scale_d3d11=width=1920:height=1080", ValueAfter(args, "-vf"));
            Assert.Equal("scale_d3d11=width=1920:height=1080:format=nv12,hwmap=derive_device=qsv,format=qsv",
                ValueAfter(Build(Plan(source: source, kind: EncoderKind.Qsv, scale: OutputScale.P1080)), "-vf"));
        }

        [Fact]
        public void SizeLimit_SharedWithEveryAudioTrack_ReachesTheEncoder()
        {
            var mixed = Plan() with { MaxGigabytesPerHour = 1 };
            var separate = Plan(audio: AudioLayout.SeparateTracks) with { MaxGigabytesPerHour = 1 };
            var silent = Plan(audio: AudioLayout.None) with { MaxGigabytesPerHour = 1 };

            Assert.Equal(2062, mixed.EncodeOptions.MaxVideoKbps);
            Assert.Equal(1902, separate.EncodeOptions.MaxVideoKbps);
            Assert.Equal(2222, silent.EncodeOptions.MaxVideoKbps);
            Assert.Null(Plan().EncodeOptions.MaxVideoKbps);
            Assert.Equal("2062k", ValueAfter(Build(mixed), "-maxrate"));
        }

        [Fact]
        public void Downscale_NeverScalesUp()
        {
            var plan = Plan(scale: OutputScale.P1080);

            Assert.Equal((1920, 1080), plan.OutputSize);
            Assert.DoesNotContain("-vf", Build(plan));
        }

        [Fact]
        public void Downscale_OddAspect_KeepsWidthEven()
        {
            var ultrawide = new MonitorTarget(@"\\.\DISPLAY3", new PixelRect(0, 0, 3440, 1440), 0, 3, GpuVendor.Amd, false);

            Assert.Equal((1720, 720), Plan(source: new MonitorSource(ultrawide), scale: OutputScale.P720).OutputSize);
        }

        [Fact]
        public void Ddagrab_Region_PassesOffsetsRelativeToItsMonitor()
        {
            var region = new RegionSource(Monitors.Second(), new PixelRect(2000, 100, 1281, 721));

            var input = ValueAfter(Build(Plan(source: region)), "-i");

            Assert.Equal("ddagrab=output_idx=1:framerate=60:draw_mouse=1:video_size=1280x720:offset_x=80:offset_y=100", input);
        }

        [Fact]
        public void Ddagrab_FullMonitor_HasNoRegionOptions()
        {
            Assert.Equal("ddagrab=output_idx=0:framerate=60:draw_mouse=1", ValueAfter(Build(Plan()), "-i"));
        }

        [Fact]
        public void Gfxcapture_Window_CapturesByHandleAndForcesConstantFrameRate()
        {
            var window = new WindowSource(0x2A3F0, "Liên Minh Huyền Thoại", new PixelRect(100, 100, 1601, 901), Monitors.Primary());

            var args = Build(Plan(source: window, backend: CaptureBackend.Gfxcapture));

            Assert.Equal("gfxcapture=hwnd=173040:max_framerate=60:capture_cursor=1:display_border=0:width=-2:height=-2", ValueAfter(args, "-i"));
            Assert.Equal("cfr", ValueAfter(args, "-fps_mode"));
            Assert.Equal("60", ValueAfter(args, "-r"));
        }

        [Fact]
        public void Gfxcapture_Region_CropsTheMonitor()
        {
            var region = new RegionSource(Monitors.Second(ddaOutput: null), new PixelRect(2000, 100, 1280, 720));

            var input = ValueAfter(Build(Plan(source: region, backend: CaptureBackend.Gfxcapture)), "-i");

            Assert.Equal("gfxcapture=hmonitor=65538:max_framerate=60:capture_cursor=1:display_border=0:crop_left=80:crop_top=100:crop_right=1200:crop_bottom=620", input);
        }

        [Fact]
        public void Ddagrab_WindowFallback_ClipsTheWindowToItsMonitor()
        {
            var window = new WindowSource(1, "Half off screen", new PixelRect(-300, 50, 1000, 600), Monitors.Primary());

            var input = ValueAfter(Build(Plan(source: window)), "-i");

            Assert.EndsWith("video_size=700x600:offset_x=0:offset_y=50", input);
        }

        [Fact]
        public void CommandLine_QuotesPathsWithSpacesAndVietnamese()
        {
            var line = RecordingCommand.Build(Plan(), @"D:\Video\Quay màn hình\001.mkv", Pipes(Plan())).ToCommandLine();

            Assert.EndsWith("\"D:\\Video\\Quay màn hình\\001.mkv\"", line);
        }

        [Fact]
        public void EncoderTest_ReusesTheRecordingChainWithoutAudio()
        {
            var plan = Plan(kind: EncoderKind.Qsv);

            var args = ProbeCommand.EncoderTest(plan).Items.ToList();

            Assert.Equal(ValueAfter(Build(plan), "-vf"), ValueAfter(args, "-vf"));
            Assert.Equal("h264_qsv", ValueAfter(args, "-c:v"));
            Assert.Contains("-an", args);
            Assert.Equal("-", args[args.Count - 1]);
        }

        private static string ValueAfter(List<string> args, string option, bool last = false)
        {
            var index = last ? args.LastIndexOf(option) : args.IndexOf(option);
            Assert.True(index >= 0, $"{option} missing in: {string.Join(" ", args)}");
            return args[index + 1];
        }
    }
}

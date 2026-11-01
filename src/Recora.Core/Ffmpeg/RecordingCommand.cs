using System;
using System.Collections.Generic;
using System.Globalization;
using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Recording;

namespace Recora.Core.Ffmpeg
{
    /// <summary>
    /// Builds the FFmpeg command for one recording segment. Video is input 0 and the audio pipes follow it, so FFmpeg
    /// grabs the first frame before it opens the pipes and sound lines up with the picture (ADR 4).
    /// </summary>
    public static class RecordingCommand
    {
        public const int AudioSampleRate = 48000;

        public const int AudioChannels = 2;

        /// <summary>Downloaded game frames are scaled next to the game on the CPU: speed beats a slightly sharper picture.</summary>
        private const string FastScaler = "fast_bilinear";

        public static FfmpegArgs Build(RecordingPlan plan, string segmentPath, IReadOnlyList<string> audioPipes) =>
            // If Recora or the PC dies mid-recording, the segment must already be on disk (BR-01): every packet is
            // handed to the OS as soon as it is muxed (otherwise FFmpeg holds 32 KB, a short recording's whole file),
            // and short Matroska clusters keep the loss to about 2 seconds.
            Encode(plan, audioPipes).Add(
                "-max_muxing_queue_size", "4096",
                "-flush_packets", "1",
                "-f", "matroska", "-cluster_time_limit", "2000",
                segmentPath);

        /// <summary>Inputs, mapping, video and audio encoding: everything but the muxer, shared with the replay buffer.</summary>
        internal static FfmpegArgs Encode(RecordingPlan plan, IReadOnlyList<string> audioPipes)
        {
            if (audioPipes.Count != plan.AudioTrackCount)
            {
                throw new ArgumentException(
                    $"{plan.Audio} needs {plan.AudioTrackCount} audio pipes, got {audioPipes.Count}", nameof(audioPipes));
            }

            var args = new FfmpegArgs()
                .Add("-hide_banner", "-y", "-nostats", "-progress", "pipe:1", "-stats_period", "0.5")
                .Add(VideoInput(plan));

            foreach (var pipe in audioPipes)
            {
                args.Add(AudioPipeInput(pipe));
            }

            args.Add("-map", "0:v:0");
            for (var i = 0; i < audioPipes.Count; i++)
            {
                args.Add("-map", Invariant($"{i + 1}:a:0"));
            }

            args.Add(VideoOutput(plan));

            if (audioPipes.Count > 0)
            {
                args.Add("-c:a", "aac", "-b:a", Invariant($"{VideoEncoderArgs.AudioBitrateKbps(plan.Quality)}k"));
                if (plan.Audio == AudioLayout.SeparateTracks)
                {
                    args.Add("-metadata:s:a:0", "title=Tiếng máy", "-metadata:s:a:1", "title=Micro");
                }
            }

            return args;
        }

        /// <summary>The raw PCM the app's AudioPump writes: 48 kHz stereo float, read with minimal probing.</summary>
        public static string[] AudioPipeInput(string pipePath) => new[]
        {
            "-thread_queue_size", "1024", "-probesize", "32", "-analyzeduration", "0",
            "-f", "f32le", "-ar", Invariant($"{AudioSampleRate}"), "-ac", Invariant($"{AudioChannels}"), "-i", pipePath,
        };

        /// <summary>
        /// Input 0 for the plan's backend: GPU capture through the lavfi device, or GDI on the CPU. Probing is cut to one
        /// frame: video time 0 is the first captured frame, and the audio pipe only opens once probing ends, so every
        /// probed frame would push the sound late (CI measured 0.67 s with a 100 MB probe).
        /// </summary>
        public static IReadOnlyList<string> VideoInput(RecordingPlan plan)
        {
            var fps = Invariant($"{plan.Fps}");
            var cursor = plan.DrawCursor ? "1" : "0";
            var area = plan.CaptureSize;
            var monitor = plan.Source.Monitor;

            switch (plan.Backend)
            {
                case CaptureBackend.Ddagrab:
                    var dda = Invariant($"ddagrab=output_idx={monitor.DdaOutputIndex ?? 0}:framerate={fps}:draw_mouse={cursor}");
                    if (plan.Source is not MonitorSource)
                    {
                        // ddagrab offsets are relative to the output, not to the virtual desktop.
                        dda += Invariant(
                            $":video_size={area.Width}x{area.Height}:offset_x={area.X - monitor.Bounds.X}:offset_y={area.Y - monitor.Bounds.Y}");
                    }

                    return new[] { "-probesize", "32", "-analyzeduration", "0", "-f", "lavfi", "-i", dda };

                case CaptureBackend.Gfxcapture:
                    return new[] { "-probesize", "32", "-analyzeduration", "0", "-f", "lavfi", "-i", GfxcaptureSource(plan, fps, cursor) };

                case CaptureBackend.Dshow:
                    // The device keeps its own size and rate (asking for one it lacks fails to open); the filter evens
                    // and scales. A big real-time buffer absorbs hiccups instead of dropping frames.
                    var device = (DeviceSource)plan.Source;
                    return new[]
                    {
                        "-thread_queue_size", "512", "-rtbufsize", "256M", "-probesize", "32", "-analyzeduration", "0",
                        "-f", "dshow", "-i", "video=" + device.DeviceName,
                    };

                default:
                    var gdi = new List<string>
                    {
                        "-thread_queue_size", "512", "-probesize", "32", "-analyzeduration", "0",
                        "-f", "gdigrab", "-framerate", fps, "-draw_mouse", cursor,
                    };
                    gdi.AddRange(new[]
                    {
                        "-offset_x", Invariant($"{area.X}"), "-offset_y", Invariant($"{area.Y}"),
                        "-video_size", Invariant($"{area.Width}x{area.Height}"), "-i", "desktop",
                    });
                    return gdi;
            }
        }

        /// <summary>Filters, frame-rate handling, encoder settings and color tags shared by recording and probing.</summary>
        public static IReadOnlyList<string> VideoOutput(RecordingPlan plan)
        {
            var output = new List<string>();
            var chain = VideoFilterChain(plan);

            if (chain.Length > 0)
            {
                output.AddRange(new[] { "-vf", chain });
            }

            if (plan.Backend == CaptureBackend.Gfxcapture)
            {
                // Windows.Graphics.Capture only delivers frames when something changes; editors want constant frame rate.
                output.AddRange(new[] { "-fps_mode", "cfr", "-r", Invariant($"{plan.Fps}") });
            }
            else if (plan.Backend == CaptureBackend.Dshow)
            {
                // Constant rate at the device's own rate: a 30 fps webcam recorded "at 60" would only repeat frames.
                output.AddRange(new[] { "-fps_mode", "cfr" });
            }

            output.AddRange(VideoEncoderArgs.Build(plan.EncodeOptions));
            output.AddRange(ColorTags(plan));
            return output;
        }

        public static string VideoFilterChain(RecordingPlan plan)
        {
            var (width, height) = plan.OutputSize;
            var capture = plan.CaptureSize;
            var scaled = width != capture.Width || height != capture.Height;
            var kind = plan.Encoder.Kind;
            var gpuFrames = plan.Backend != CaptureBackend.Gdigrab && plan.Backend != CaptureBackend.Dshow;

            if (plan.Backend == CaptureBackend.Dshow)
            {
                return DeviceConversion(kind, plan.Scale);
            }

            if (!gpuFrames)
            {
                return CpuConversion(kind, scaled, width, height);
            }

            var gpuSize = scaled ? Invariant($"width={width}:height={height}:") : string.Empty;

            switch (plan.Route)
            {
                case FrameRoute.Nv12Textures:
                    return $"scale_d3d11={gpuSize}format=nv12";

                case FrameRoute.Nv12Memory:
                    return $"scale_d3d11={gpuSize}format=nv12,hwdownload,format=nv12";

                case FrameRoute.BgraMemory:
                    return "hwdownload,format=bgra," + CpuConversion(kind, scaled, width, height, FastScaler);
            }

            switch (kind)
            {
                case EncoderKind.Nvenc:
                case EncoderKind.Amf:
                    // Both take BGRA D3D11 textures directly: the frame never leaves the GPU.
                    return scaled ? Invariant($"scale_d3d11=width={width}:height={height}") : string.Empty;

                case EncoderKind.Qsv:
                    return $"scale_d3d11={gpuSize}format=nv12,hwmap=derive_device=qsv,format=qsv";

                default:
                    return "hwdownload,format=bgra," + CpuConversion(kind, scaled, width, height, FastScaler);
            }
        }

        /// <summary>
        /// Device frames (YUY2, MJPEG, NV12...) of a size only known once open: round to even, cap the height, convert.
        /// No color matrix is forced: webcams send BT.601 YUV and swscale keeps it as it is.
        /// </summary>
        private static string DeviceConversion(EncoderKind kind, OutputScale scale)
        {
            var height = scale == OutputScale.Original ? "trunc(ih/2)*2" : Invariant($"trunc(min(ih\\,{(int)scale})/2)*2");
            var format = kind == EncoderKind.X264 ? "yuv420p" : "nv12";
            return $"scale=-2:{height},format={format}";
        }

        private static string CpuConversion(EncoderKind kind, bool scaled, int width, int height, string scaler = "bicubic")
        {
            var size = scaled ? Invariant($"{width}:{height}:flags={scaler}:") : string.Empty;
            var format = kind == EncoderKind.X264 ? "yuv420p" : "nv12";
            return $"scale={size}out_color_matrix=bt709:out_range=tv,format={format}";
        }

        /// <summary>
        /// Tags only where the conversion is known: swscale is told BT.709, the D3D11 video processor converts with
        /// BT.601. NVENC tags RGB input itself (nvenc.c), and AMF's converter is left to the driver.
        /// </summary>
        private static IEnumerable<string> ColorTags(RecordingPlan plan)
        {
            if (plan.Backend == CaptureBackend.Dshow)
            {
                return new string[0];
            }

            if (plan.Backend == CaptureBackend.Gdigrab || plan.Route == FrameRoute.BgraMemory || plan.Encoder.Kind == EncoderKind.X264 || plan.Encoder.Kind == EncoderKind.MediaFoundation)
            {
                return new[] { "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv" };
            }

            if (plan.Encoder.Kind == EncoderKind.Qsv || plan.Route == FrameRoute.Nv12Textures || plan.Route == FrameRoute.Nv12Memory)
            {
                return new[] { "-colorspace", "bt470bg", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv" };
            }

            return new string[0];
        }

        private static string GfxcaptureSource(RecordingPlan plan, string fps, string cursor)
        {
            var common = $"max_framerate={fps}:capture_cursor={cursor}:display_border=0";

            if (plan.Source is WindowSource window)
            {
                // Negative sizes round down to a multiple of 2, keeping odd-sized windows encodable.
                return Invariant($"gfxcapture=hwnd={window.Hwnd}:{common}:width=-2:height=-2");
            }

            var monitor = plan.Source.Monitor;
            var source = Invariant($"gfxcapture=hmonitor={monitor.HMonitor}:{common}");

            if (plan.Source is RegionSource)
            {
                var area = plan.CaptureSize;
                var bounds = monitor.Bounds;
                source += Invariant(
                    $":crop_left={area.X - bounds.X}:crop_top={area.Y - bounds.Y}:crop_right={bounds.Right - area.Right}:crop_bottom={bounds.Bottom - area.Bottom}");
            }
            else
            {
                source += ":width=-2:height=-2";
            }

            return source;
        }

        private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

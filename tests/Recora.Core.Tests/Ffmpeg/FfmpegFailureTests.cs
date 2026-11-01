using Recora.Core.Ffmpeg;

namespace Recora.Core.Tests.Ffmpeg
{
    public class FfmpegFailureTests
    {
        [Fact]
        public void Reason_WhenEncoderBreaksThenFfmpegKeepsPrinting_NamesTheEncoder()
        {
            var stderr = string.Join("\n",
                "[D3D11VA @ 000001] AMF via D3D11.",
                "[h264_amf @ 000001] SubmitInput() failed with error 18",
                "[vost#0:0/h264_amf @ 000002] Error submitting video frame to the encoder",
                "Output #0, segment, to 'chunk%03d.mkv':",
                "frame=    0 fps=0.0 q=0.0 Lsize=N/A time=N/A bitrate=N/A speed=N/A",
                "[aac @ 000003] Qavg: nan",
                "[aac @ 000004] Qavg: nan",
                "Conversion failed!");

            var reason = FfmpegFailure.Reason(stderr);

            Assert.StartsWith("[h264_amf @ 000001] SubmitInput() failed with error 18", reason);
            Assert.DoesNotContain("Qavg", reason);
            Assert.DoesNotContain("Conversion failed", reason);
        }

        [Fact]
        public void Reason_WhenInputCannotOpen_KeepsTheTextTheRunnerLooksFor()
        {
            var reason = FfmpegFailure.Reason("Error opening input: I/O error\nError opening input files: I/O error\n");

            Assert.Contains("Error opening input", reason);
        }

        [Fact]
        public void Reason_WhenNoLineLooksLikeAnError_FallsBackToTheLastLines()
        {
            Assert.Equal("c | d | e | f", FfmpegFailure.Reason("a\nb\nc\nd\ne\nf\n"));
        }

        [Fact]
        public void Reason_WhenStderrIsEmpty_SaysFfmpegExited()
        {
            Assert.Equal("ffmpeg exited", FfmpegFailure.Reason("  \n"));
        }
    }
}

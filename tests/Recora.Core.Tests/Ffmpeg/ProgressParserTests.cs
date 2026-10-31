using Recora.Core.Ffmpeg;

namespace Recora.Core.Tests.Ffmpeg
{
    public class ProgressParserTests
    {
        [Fact]
        public void Feed_ProgressBlock_ReturnsSnapshotAtBlockEnd()
        {
            var parser = new ProgressParser();
            var lines = new[]
            {
                "frame=120", "fps=60.00", "stream_0_0_q=24.0", "bitrate=8000.0kbits/s", "total_size=2000000",
                "out_time_us=2000000", "out_time_ms=2000000", "out_time=00:00:02.000000", "dup_frames=0", "drop_frames=3",
                "speed=1.00x",
            };

            Assert.All(lines, line => Assert.Null(parser.Feed(line)));
            var progress = parser.Feed("progress=continue");

            Assert.NotNull(progress);
            Assert.Equal(120, progress!.Frame);
            Assert.Equal(60.0, progress.Fps);
            Assert.Equal(2_000_000, progress.TotalSizeBytes);
            Assert.Equal(TimeSpan.FromSeconds(2), progress.OutTime);
            Assert.Equal(3, progress.DroppedFrames);
            Assert.False(progress.IsEnd);
        }

        [Fact]
        public void Feed_NotAvailableValues_KeepPreviousNumbers()
        {
            var parser = new ProgressParser();
            parser.Feed("out_time_us=1000000");
            parser.Feed("total_size=500");
            parser.Feed("progress=continue");

            parser.Feed("out_time_us=N/A");
            parser.Feed("total_size=N/A");
            var progress = parser.Feed("progress=end");

            Assert.Equal(TimeSpan.FromSeconds(1), progress!.OutTime);
            Assert.Equal(500, progress.TotalSizeBytes);
            Assert.True(progress.IsEnd);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Press [q] to stop")]
        [InlineData("=value")]
        public void Feed_NonProgressLines_AreIgnored(string line)
        {
            Assert.Null(new ProgressParser().Feed(line));
        }
    }
}

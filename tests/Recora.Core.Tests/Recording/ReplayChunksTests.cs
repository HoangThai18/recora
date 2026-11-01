using System.Linq;
using Recora.Core.Recording;

namespace Recora.Core.Tests.Recording
{
    public class ReplayChunksTests
    {
        private const string Full = "chunk003.mkv,6.000000,8.016667\nchunk004.mkv,8.016667,10.000000\nchunk005.mkv,10.000000,12.016667\nchunk000.mkv,12.016667,14.000000\n";

        [Fact]
        public void Parse_ReadsNamesAndTimesInOrder()
        {
            var chunks = ReplayChunks.Parse(Full);

            Assert.Equal(new[] { "chunk003.mkv", "chunk004.mkv", "chunk005.mkv", "chunk000.mkv" }, chunks.Select(c => c.FileName));
            Assert.Equal(1.983333, chunks[3].Seconds, 5);
        }

        [Fact]
        public void Parse_SkipsBrokenLinesAndUnquotesNames()
        {
            var chunks = ReplayChunks.Parse("\"a,b.mkv\",0.0,2.0\r\ngarbage\nchunk001.mkv,2.0,\n,1,2\nchunk002.mkv,2.0,4.0");

            Assert.Equal(new[] { "a,b.mkv", "chunk002.mkv" }, chunks.Select(c => c.FileName));
        }

        [Fact]
        public void Pick_TakesTheNewestChunksThatCoverTheLength()
        {
            var picked = ReplayChunks.Pick(ReplayChunks.Parse(Full), 3, maxChunks: 10);

            Assert.Equal(new[] { "chunk005.mkv", "chunk000.mkv" }, picked.Select(c => c.FileName));
        }

        [Fact]
        public void Pick_ShortBuffer_TakesEverything()
        {
            Assert.Equal(4, ReplayChunks.Pick(ReplayChunks.Parse(Full), 60, maxChunks: 10).Count);
            Assert.Empty(ReplayChunks.Pick(ReplayChunks.Parse(string.Empty), 30, maxChunks: 10));
        }

        [Fact]
        public void Pick_NeverReachesTheChunksFfmpegIsAboutToReuse()
        {
            var picked = ReplayChunks.Pick(ReplayChunks.Parse(Full), 60, maxChunks: ReplayChunks.SafePickCount(5));

            Assert.Equal(new[] { "chunk005.mkv", "chunk000.mkv" }, picked.Select(c => c.FileName));
            Assert.Equal(17, ReplayChunks.SafePickCount(20));
        }
    }
}

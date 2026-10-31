using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;

namespace Recora.Core.Tests.Ffmpeg
{
    public class FinalizeCommandTests
    {
        [Fact]
        public void ConcatList_QuotesPathsAndEscapesSingleQuotes()
        {
            var list = FinalizeCommand.ConcatList(new[]
            {
                @"D:\Video\Quay màn hình\.recora\s1\001.mkv",
                @"D:\Video\Tom's clips\002.mkv",
            });

            Assert.Equal(
                "ffconcat version 1.0\n" +
                "file 'D:\\Video\\Quay màn hình\\.recora\\s1\\001.mkv'\n" +
                "file 'D:\\Video\\Tom'\\''s clips\\002.mkv'\n",
                list);
        }

        [Fact]
        public void ConcatList_WithOutPoints_CutsEachSegmentAtItsAudioLength()
        {
            var list = FinalizeCommand.ConcatList(new[]
            {
                new ConcatEntry(@"C:\s\001.mkv", 2.5),
                new ConcatEntry(@"C:\s\002.mkv", 1.0004),
                new ConcatEntry(@"C:\s\003.mkv"),
            });

            Assert.Equal(
                "ffconcat version 1.0\n" +
                "file 'C:\\s\\001.mkv'\noutpoint 2.500\n" +
                "file 'C:\\s\\002.mkv'\noutpoint 1.000\n" +
                "file 'C:\\s\\003.mkv'\n",
                list);
        }

        [Fact]
        public void Build_Mp4WithHevc_TagsHvc1AndCopiesStreams()
        {
            var args = FinalizeCommand.Build("list.txt", "out.mp4", ContainerFormat.Mp4, VideoCodec.Hevc).Items.ToList();

            Assert.Equal(new[] { "-f", "concat", "-safe", "0", "-i", "list.txt" }, args.Skip(3).Take(6));
            Assert.Equal("copy", args[args.IndexOf("-c") + 1]);
            Assert.Equal("0", args[args.IndexOf("-map") + 1]);
            Assert.Equal("hvc1", args[args.IndexOf("-tag:v") + 1]);
            Assert.Equal(new[] { "-f", "mp4", "out.mp4" }, args.Skip(args.Count - 3));
        }

        [Fact]
        public void Build_Mkv_NeedsNoTag()
        {
            var args = FinalizeCommand.Build("list.txt", "out.mkv", ContainerFormat.Mkv, VideoCodec.Hevc).Items.ToList();

            Assert.DoesNotContain("-tag:v", args);
            Assert.Equal(new[] { "-f", "matroska", "out.mkv" }, args.Skip(args.Count - 3));
        }

        [Theory]
        [InlineData(ContainerFormat.Mkv, 1, false, true)]
        [InlineData(ContainerFormat.Mkv, 2, false, false)]
        [InlineData(ContainerFormat.Mkv, 1, true, false)]
        [InlineData(ContainerFormat.Mp4, 1, false, false)]
        public void CanMoveInsteadOfRemux_OnlyForOneCleanMkvSegment(ContainerFormat format, int segments, bool recovered, bool expected)
        {
            var paths = Enumerable.Range(1, segments).Select(i => $"{i:000}.mkv").ToList();

            Assert.Equal(expected, FinalizeCommand.CanMoveInsteadOfRemux(paths, format, recovered));
        }
    }
}

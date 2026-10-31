using System.Text;
using Recora.Core.Ffmpeg;

namespace Recora.Core.Tests.Ffmpeg
{
    public class FfmpegArgsTests
    {
        [Theory]
        [InlineData("abc", "abc")]
        [InlineData("", "\"\"")]
        [InlineData("a b", "\"a b\"")]
        [InlineData("a\"b", "\"a\\\"b\"")]
        [InlineData(@"C:\dir\file.mp4", @"C:\dir\file.mp4")]
        [InlineData(@"C:\path with space\", "\"C:\\path with space\\\\\"")]
        [InlineData("a\\\\\"b", "\"a\\\\\\\\\\\"b\"")]
        public void Quote_FollowsCommandLineToArgvRules(string arg, string expected)
        {
            Assert.Equal(expected, FfmpegArgs.Quote(arg));
        }

        [Theory]
        [InlineData(@"D:\Video\Quay màn hình\Recora 2026-10-31 20-15-03.mkv")]
        [InlineData("ddagrab=output_idx=0:framerate=60:draw_mouse=1")]
        [InlineData(@"\\.\pipe\recora-1234-1-audio")]
        [InlineData("tab\there")]
        [InlineData("ends with backslash \\")]
        [InlineData("\"")]
        [InlineData("\\\\server\\share\\x y\\\\")]
        public void ToCommandLine_RoundTripsThroughWindowsParser(string tricky)
        {
            var args = new FfmpegArgs().Add("-i", tricky, "-y", "", "out put.mp4");

            Assert.Equal(args.Items, ParseLikeCommandLineToArgvW(args.ToCommandLine()));
        }

        [Fact]
        public void ValueOf_ReturnsArgumentAfterOption()
        {
            var args = new FfmpegArgs().Add("-c:v", "h264_nvenc", "-g", "120");

            Assert.Equal("h264_nvenc", args.ValueOf("-c:v"));
            Assert.Null(args.ValueOf("-b:v"));
        }

        [Fact]
        public void AddIf_SkipsWhenConditionFalse()
        {
            var args = new FfmpegArgs().AddIf(false, "-an").AddIf(true, "-sn");

            Assert.Equal(new[] { "-sn" }, args.Items);
        }

        /// <summary>Reference split from the MSVCRT/CommandLineToArgvW rules ffmpeg.exe applies on Windows.</summary>
        private static List<string> ParseLikeCommandLineToArgvW(string commandLine)
        {
            var result = new List<string>();
            var i = 0;

            while (i < commandLine.Length)
            {
                while (i < commandLine.Length && (commandLine[i] == ' ' || commandLine[i] == '\t'))
                {
                    i++;
                }

                if (i >= commandLine.Length)
                {
                    break;
                }

                var current = new StringBuilder();
                var inQuotes = false;

                while (i < commandLine.Length)
                {
                    var c = commandLine[i];

                    if (c == '\\')
                    {
                        var count = 0;
                        while (i < commandLine.Length && commandLine[i] == '\\')
                        {
                            count++;
                            i++;
                        }

                        if (i < commandLine.Length && commandLine[i] == '"')
                        {
                            current.Append('\\', count / 2);
                            if (count % 2 == 1)
                            {
                                current.Append('"');
                                i++;
                            }
                        }
                        else
                        {
                            current.Append('\\', count);
                        }

                        continue;
                    }

                    if (c == '"')
                    {
                        if (inQuotes && i + 1 < commandLine.Length && commandLine[i + 1] == '"')
                        {
                            current.Append('"');
                            i += 2;
                            continue;
                        }

                        inQuotes = !inQuotes;
                        i++;
                        continue;
                    }

                    if ((c == ' ' || c == '\t') && !inQuotes)
                    {
                        break;
                    }

                    current.Append(c);
                    i++;
                }

                result.Add(current.ToString());
            }

            return result;
        }
    }
}

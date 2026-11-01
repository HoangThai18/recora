using System;
using System.IO;
using System.Linq;
using System.Text;
using Recora.Core.Encoding;

namespace Recora.App
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// <c>--dump-encoder-args &lt;file&gt;</c>: every encoder argument list the app can build (each encoder and codec,
        /// each quality, with and without a size limit), one per line as <c>name&lt;TAB&gt;arg&lt;US&gt;arg…</c>. CI checks
        /// each option name and value against the shipped ffmpeg.exe's own help, which needs no GPU.
        /// </summary>
        public static int DumpEncoderArgs(string file)
        {
            var lines = new StringBuilder();
            foreach (EncoderKind kind in Enum.GetValues(typeof(EncoderKind)))
            {
                foreach (VideoCodec codec in Enum.GetValues(typeof(VideoCodec)))
                {
                    var name = EncoderCatalog.FfmpegName(kind, codec);
                    if (name == null)
                    {
                        continue;
                    }

                    foreach (QualityPreset quality in Enum.GetValues(typeof(QualityPreset)))
                    {
                        foreach (var limit in new int?[] { null, 2062 })
                        {
                            var options = new VideoEncodeOptions(new VideoEncoder(kind, codec), quality, 60, 1920, 1080, limit);
                            var args = VideoEncoderArgs.Build(options).ToArray();
                            lines.Append(name).Append('\t').Append(string.Join("\u001f", args)).Append('\n');
                        }
                    }
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
            File.WriteAllText(file, lines.ToString(), new UTF8Encoding(false));
            Console.WriteLine($"wrote {lines.ToString().Count(c => c == '\n')} encoder argument lists to {file}");
            return 0;
        }
    }
}

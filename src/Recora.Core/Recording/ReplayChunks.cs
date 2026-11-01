using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Recora.Core.Recording
{
    /// <summary>One finished chunk of the replay ring, as FFmpeg's segment list records it.</summary>
    public sealed record ReplayChunk(string FileName, double Start, double End)
    {
        public double Seconds => Math.Max(0, End - Start);
    }

    /// <summary>Reads the replay ring's CSV and picks the chunks that make up the last N seconds.</summary>
    public static class ReplayChunks
    {
        /// <summary>
        /// Lines are <c>file,start,end</c>, oldest first. A line that does not parse (a list caught mid-write by an older
        /// FFmpeg, or a stray blank) is skipped rather than failing the save.
        /// </summary>
        public static IReadOnlyList<ReplayChunk> Parse(string csv)
        {
            var chunks = new List<ReplayChunk>();
            foreach (var raw in csv.Split('\n'))
            {
                var line = raw.Trim();
                var lastComma = line.LastIndexOf(',');
                var middleComma = lastComma > 0 ? line.LastIndexOf(',', lastComma - 1) : -1;
                if (middleComma <= 0)
                {
                    continue;
                }

                var name = Unquote(line.Substring(0, middleComma));
                if (name.Length == 0
                    || !double.TryParse(line.Substring(middleComma + 1, lastComma - middleComma - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var start)
                    || !double.TryParse(line.Substring(lastComma + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var end))
                {
                    continue;
                }

                chunks.Add(new ReplayChunk(name, start, end));
            }

            return chunks;
        }

        /// <summary>
        /// The newest chunks covering at least <paramref name="seconds"/>, in playing order. At most
        /// <paramref name="maxChunks"/>: in a full ring the oldest names are the ones FFmpeg is about to overwrite.
        /// </summary>
        public static IReadOnlyList<ReplayChunk> Pick(IReadOnlyList<ReplayChunk> chunks, double seconds, int maxChunks)
        {
            var picked = new List<ReplayChunk>();
            var covered = 0.0;

            for (var i = chunks.Count - 1; i >= 0 && covered < seconds && picked.Count < maxChunks; i--)
            {
                picked.Add(chunks[i]);
                covered += chunks[i].Seconds;
            }

            picked.Reverse();
            return picked;
        }

        /// <summary>How many of the ring's chunks a save may take, leaving the ones FFmpeg is about to reuse alone.</summary>
        public static int SafePickCount(int ringSize) => Math.Max(1, ringSize - 3);

        private static string Unquote(string value)
        {
            value = value.Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                value = value.Substring(1, value.Length - 2).Replace("\"\"", "\"");
            }

            return value;
        }
    }
}

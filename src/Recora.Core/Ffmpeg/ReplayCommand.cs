using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Recora.Core.Recording;

namespace Recora.Core.Ffmpeg
{
    /// <summary>
    /// The replay buffer ("Lưu khoảnh khắc"): the same capture and encoding as a recording, written as a ring of short
    /// Matroska chunks that FFmpeg overwrites in turn, so the disk only ever holds the last few minutes. FFmpeg also
    /// keeps a CSV of the finished chunks (written to a temp file and renamed over the old one, so a reader never sees
    /// half a list), which says which chunks hold the newest seconds.
    /// </summary>
    public static class ReplayCommand
    {
        /// <summary>Chunk length. The encoder's GOP is two seconds too, so every chunk starts on a keyframe.</summary>
        public const int ChunkSeconds = 2;

        public const string ListFileName = "replay.csv";

        public const string ChunkPattern = "chunk%03d.mkv";

        /// <summary>
        /// Chunks in the ring: enough for the requested length plus one partial chunk at each end, and a margin so the
        /// chunks being copied for a save are not overwritten while FFmpeg moves on.
        /// </summary>
        public static int RingSize(int bufferSeconds) => (int)Math.Ceiling(Math.Max(1, bufferSeconds) / (double)ChunkSeconds) + 5;

        public static FfmpegArgs Build(RecordingPlan plan, string folder, IReadOnlyList<string> audioPipes, int bufferSeconds)
        {
            var ring = RingSize(bufferSeconds).ToString(CultureInfo.InvariantCulture);
            return RecordingCommand.Encode(plan, audioPipes).Add(
                "-max_muxing_queue_size", "4096",
                "-f", "segment",
                "-segment_format", "matroska",
                "-segment_time", ChunkSeconds.ToString(CultureInfo.InvariantCulture),
                "-segment_wrap", ring,
                "-segment_list", Path.Combine(folder, ListFileName),
                "-segment_list_type", "csv",
                "-segment_list_size", ring,
                "-reset_timestamps", "1",
                Path.Combine(folder, ChunkPattern));
        }
    }
}

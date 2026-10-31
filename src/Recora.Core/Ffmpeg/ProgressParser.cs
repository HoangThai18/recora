using System;
using System.Globalization;

namespace Recora.Core.Ffmpeg
{
    public sealed record FfmpegProgress(long Frame, double Fps, long TotalSizeBytes, TimeSpan OutTime, long DroppedFrames, bool IsEnd);

    /// <summary>
    /// Reads FFmpeg's <c>-progress pipe:1</c> output: <c>key=value</c> lines, each block closed by
    /// <c>progress=continue</c> or <c>progress=end</c>. Plain lines are easier to read reliably than the stderr stats
    /// line, which FFmpeg redraws with carriage returns.
    /// </summary>
    public sealed class ProgressParser
    {
        private long _frame;
        private double _fps;
        private long _size;
        private TimeSpan _outTime;
        private long _dropped;

        /// <summary>Feeds one line; returns a snapshot when the line closes a block, otherwise null.</summary>
        public FfmpegProgress? Feed(string line)
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                return null;
            }

            var key = line.Substring(0, separator).Trim();
            var value = line.Substring(separator + 1).Trim();

            switch (key)
            {
                case "frame":
                    _frame = ParseLong(value, _frame);
                    break;
                case "fps":
                    _fps = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fps) ? fps : _fps;
                    break;
                case "total_size":
                    _size = ParseLong(value, _size);
                    break;
                case "out_time_us":
                    // N/A until the first packet is muxed.
                    _outTime = long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var us) && us >= 0
                        ? TimeSpan.FromTicks(us * 10)
                        : _outTime;
                    break;
                case "drop_frames":
                    _dropped = ParseLong(value, _dropped);
                    break;
                case "progress":
                    return new FfmpegProgress(_frame, _fps, _size, _outTime, _dropped, value == "end");
            }

            return null;
        }

        private static long ParseLong(string value, long fallback) =>
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }
}

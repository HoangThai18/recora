using System;
using System.Collections.Generic;
using Recora.Core.Encoding;

namespace Recora.Core.Ffmpeg
{
    /// <summary>File types for the "Âm thanh" mode.</summary>
    public enum AudioFormat
    {
        /// <summary>AAC in MP4: small, plays everywhere.</summary>
        M4a,

        /// <summary>MP3 through Windows' Media Foundation encoder.</summary>
        Mp3,

        /// <summary>Uncompressed 16-bit PCM, for editing.</summary>
        Wav,
    }

    /// <summary>
    /// Sound-only recording: the app's mixed audio pipe encoded straight into a Matroska segment, finished later by
    /// stream copy like a video (same crash safety, same pause handling).
    /// </summary>
    public static class AudioRecordingCommand
    {
        public static FfmpegArgs Build(string audioPipe, AudioFormat format, QualityPreset quality, string segmentPath)
        {
            var args = new FfmpegArgs()
                .Add("-hide_banner", "-y", "-nostats", "-progress", "pipe:1", "-stats_period", "0.5")
                .Add(RecordingCommand.AudioPipeInput(audioPipe))
                .Add("-map", "0:a:0")
                .Add(Codec(format, quality));

            return args.Add("-flush_packets", "1", "-f", "matroska", "-cluster_time_limit", "2000", segmentPath);
        }

        public static IReadOnlyList<string> Codec(AudioFormat format, QualityPreset quality)
        {
            var bitrate = FormattableString.Invariant($"{Math.Max(160, VideoEncoderArgs.AudioBitrateKbps(quality) + 32)}k");
            return format switch
            {
                AudioFormat.Mp3 => new[] { "-c:a", "mp3_mf", "-b:a", bitrate },
                AudioFormat.Wav => new[] { "-c:a", "pcm_s16le" },
                _ => new[] { "-c:a", "aac", "-b:a", bitrate },
            };
        }

        public static string Extension(AudioFormat format) => format switch
        {
            AudioFormat.Mp3 => ".mp3",
            AudioFormat.Wav => ".wav",
            _ => ".m4a",
        };

        /// <summary>The muxer for the final file: <c>ipod</c> writes .m4a the way Apple and Android players expect.</summary>
        public static string Muxer(AudioFormat format) => format switch
        {
            AudioFormat.Mp3 => "mp3",
            AudioFormat.Wav => "wav",
            _ => "ipod",
        };
    }
}

using System;

namespace Recora.Core.Audio
{
    public static class AudioMixing
    {
        /// <summary>Adds <paramref name="source"/> times <paramref name="gain"/> onto <paramref name="mix"/>.</summary>
        public static void Accumulate(float[] mix, float[] source, float gain, int samples)
        {
            for (var i = 0; i < samples; i++)
            {
                mix[i] += source[i] * gain;
            }
        }

        /// <summary>Hard-limits to [-1, 1]: two loud sources summed would otherwise wrap into harsh noise in AAC.</summary>
        public static void Clamp(float[] mix, int samples)
        {
            for (var i = 0; i < samples; i++)
            {
                var v = mix[i];
                mix[i] = float.IsNaN(v) ? 0f : v > 1f ? 1f : v < -1f ? -1f : v;
            }
        }

        /// <summary>Stereo frames from any channel layout: mono is duplicated, surround keeps front left and right.</summary>
        public static void ToStereo(float[] source, int channels, float[] stereo, int frames)
        {
            if (channels == 2)
            {
                Array.Copy(source, stereo, frames * 2);
                return;
            }

            for (var f = 0; f < frames; f++)
            {
                var left = source[f * channels];
                var right = channels == 1 ? left : source[f * channels + 1];
                stereo[f * 2] = left;
                stereo[f * 2 + 1] = right;
            }
        }
    }

    /// <summary>
    /// Decides how many frames the pump owes FFmpeg: exactly elapsed time times the sample rate, whatever the timer
    /// jitter. The audio track therefore lasts as long as the wall clock, matching ddagrab's wall-clock video.
    /// </summary>
    public sealed class AudioPacer
    {
        private readonly int _sampleRate;
        private readonly int _maxChunkFrames;
        private long _framesSent;

        public AudioPacer(int sampleRate, int maxChunkFrames)
        {
            _sampleRate = sampleRate;
            _maxChunkFrames = maxChunkFrames;
        }

        public long FramesSent => _framesSent;

        /// <summary>Frames to send now, capped per call so one late wake-up doesn't produce a giant write.</summary>
        public int Due(TimeSpan elapsed)
        {
            var target = (long)(elapsed.Ticks * (double)_sampleRate / TimeSpan.TicksPerSecond);
            var due = target - _framesSent;
            return due <= 0 ? 0 : (int)Math.Min(due, _maxChunkFrames);
        }

        public void Sent(int frames) => _framesSent += frames;
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Recora.App.Logging;
using Recora.Core.Audio;
using Recora.Core.Ffmpeg;

namespace Recora.App.Audio
{
    /// <summary>
    /// The Âm thanh page's test buttons, like streaming software has: record the microphone for a few seconds and play
    /// it back through the chosen speakers after the same processing a recording gets, and a chime left then right.
    /// Every method blocks; call them from the thread pool.
    /// </summary>
    internal static class AudioCheck
    {
        private const int Rate = RecordingCommand.AudioSampleRate;

        /// <summary>Records <paramref name="length"/> of the microphone, reporting the peak of each block as it comes.</summary>
        public static float[] RecordMicrophone(MMDevice microphone, TimeSpan length, Action<float> level, CancellationToken cancel)
        {
            var samples = new List<float>((int)(length.TotalSeconds * Rate * 2));
            var wanted = (int)(length.TotalSeconds * Rate) * 2;
            using (var done = new ManualResetEventSlim())
            using (var source = WasapiSource.Microphone(microphone))
            {
                source.Captured += (block, frames) =>
                {
                    var peak = 0f;
                    lock (samples)
                    {
                        for (var i = 0; i < frames * 2 && samples.Count < wanted; i++)
                        {
                            samples.Add(block[i]);
                            peak = Math.Max(peak, Math.Abs(block[i]));
                        }

                        if (samples.Count >= wanted)
                        {
                            done.Set();
                        }
                    }

                    level(peak);
                };
                source.Start();
                WaitHandle.WaitAny(new[] { done.WaitHandle, cancel.WaitHandle }, length + TimeSpan.FromSeconds(3));
            }

            lock (samples)
            {
                return samples.ToArray();
            }
        }

        /// <summary>What a recording would hear: the user's chain, then the microphone volume, clamped like the mix.</summary>
        public static float[] Process(float[] stereo, MicProcessingSettings settings, float volume)
        {
            var output = (float[])stereo.Clone();
            new MicProcessor(settings, Rate).Process(output, output.Length / 2);
            for (var i = 0; i < output.Length; i++)
            {
                var v = output[i] * volume;
                output[i] = v > 1f ? 1f : v < -1f ? -1f : v;
            }

            return output;
        }

        /// <summary>A soft two-note chime on the left only, then the same on the right only.</summary>
        public static float[] Chime()
        {
            var note = (int)(0.45 * Rate);
            var gap = (int)(0.25 * Rate);
            var output = new float[(note * 2 + gap * 2) * 2];

            void Ring(int start, int channel)
            {
                for (var f = 0; f < note; f++)
                {
                    var t = f / (double)Rate;
                    var frequency = f < note / 2 ? 880.0 : 1318.5;
                    var envelope = Math.Min(1.0, t * 80) * Math.Exp(-4.0 * (f % (note / 2)) / Rate * 2);
                    output[(start + f) * 2 + channel] = (float)(Math.Sin(2 * Math.PI * frequency * t) * 0.35 * envelope);
                }
            }

            Ring(0, 0);
            Ring(note + gap, 1);
            return output;
        }

        /// <summary>Plays 48 kHz stereo floats on a speaker and returns when they have finished (or on cancel).</summary>
        public static void Play(MMDevice speaker, float[] stereo, CancellationToken cancel)
        {
            var provider = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2))
            {
                BufferLength = stereo.Length * sizeof(float) + Rate * 8,
                ReadFully = false,
            };
            var bytes = new byte[stereo.Length * sizeof(float)];
            Buffer.BlockCopy(stereo, 0, bytes, 0, bytes.Length);
            provider.AddSamples(bytes, 0, bytes.Length);

            using (var finished = new ManualResetEventSlim())
            using (var output = new WasapiOut(speaker, AudioClientShareMode.Shared, true, 100))
            {
                output.PlaybackStopped += (_, e) =>
                {
                    if (e.Exception != null)
                    {
                        Log.Warn("test playback stopped: " + e.Exception.Message);
                    }

                    finished.Set();
                };
                output.Init(provider);
                output.Play();
                var seconds = stereo.Length / 2.0 / Rate;
                WaitHandle.WaitAny(new[] { finished.WaitHandle, cancel.WaitHandle }, TimeSpan.FromSeconds(seconds + 3));
                output.Stop();
            }
        }
    }
}

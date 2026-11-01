using System;
using System.Linq;
using Recora.Core.Audio;

namespace Recora.Core.Tests.Audio
{
    public class MicProcessorTests
    {
        private const int Rate = 48000;

        /// <summary>Stereo sine at <paramref name="db"/> dBFS peak, the same on both channels.</summary>
        private static float[] Tone(double frequency, float db, double seconds)
        {
            var frames = (int)(seconds * Rate);
            var amplitude = MicProcessor.DbToLinear(db);
            var stereo = new float[frames * 2];
            for (var f = 0; f < frames; f++)
            {
                var v = (float)(Math.Sin(2 * Math.PI * frequency * f / Rate) * amplitude);
                stereo[f * 2] = v;
                stereo[f * 2 + 1] = v;
            }

            return stereo;
        }

        /// <summary>Deterministic white noise at roughly <paramref name="db"/> dBFS peak.</summary>
        private static float[] Noise(float db, double seconds)
        {
            var random = new Random(7);
            var amplitude = MicProcessor.DbToLinear(db);
            var stereo = new float[(int)(seconds * Rate) * 2];
            for (var i = 0; i < stereo.Length; i += 2)
            {
                var v = (float)((random.NextDouble() * 2 - 1) * amplitude);
                stereo[i] = v;
                stereo[i + 1] = v;
            }

            return stereo;
        }

        /// <summary>Runs the processor in 100 ms blocks like the pump does, so state must carry across blocks.</summary>
        private static float[] Run(MicProcessingSettings settings, float[] input)
        {
            var processor = new MicProcessor(settings, Rate);
            var output = (float[])input.Clone();
            var block = new float[Rate / 10 * 2];
            for (var start = 0; start < output.Length; start += block.Length)
            {
                var count = Math.Min(block.Length, output.Length - start);
                Array.Copy(output, start, block, 0, count);
                processor.Process(block, count / 2);
                Array.Copy(block, 0, output, start, count);
            }

            return output;
        }

        /// <summary>Peak level in dBFS of the last <paramref name="seconds"/> (after filters and envelopes settle).</summary>
        private static float TailPeakDb(float[] stereo, double seconds)
        {
            var tail = stereo.Skip(Math.Max(0, stereo.Length - (int)(seconds * Rate) * 2));
            return MicProcessor.LinearToDb(tail.Select(Math.Abs).Max());
        }

        private static MicProcessingSettings Off() => new MicProcessingSettings { Limiter = false };

        [Fact]
        public void AllOff_LeavesTheSoundUntouched()
        {
            var input = Noise(-10, 0.5);

            Assert.True(new MicProcessor(Off(), Rate).IsBypass);
            Assert.Equal(input, Run(Off(), input));
        }

        [Fact]
        public void Gain_BoostsByTheChosenDecibels()
        {
            var output = Run(new MicProcessingSettings { Limiter = false, GainDb = 12 }, Tone(1000, -30, 0.5));

            Assert.InRange(TailPeakDb(output, 0.2), -18.1f, -17.9f);
        }

        [Fact]
        public void HighPass_CutsRumbleAndKeepsTheVoice()
        {
            var settings = new MicProcessingSettings { Limiter = false, HighPass = true };

            Assert.True(TailPeakDb(Run(settings, Tone(30, -12, 1.0)), 0.5) <= -24f, "30 Hz must drop at least 12 dB");
            Assert.InRange(TailPeakDb(Run(settings, Tone(1000, -12, 1.0)), 0.5), -13f, -11f);
        }

        [Fact]
        public void NoiseGate_SilencesRoomNoiseBelowTheThreshold()
        {
            var settings = new MicProcessingSettings { Limiter = false, NoiseGate = true, GateThresholdDb = -45 };

            Assert.True(TailPeakDb(Run(settings, Noise(-55, 1.5)), 0.5) < -80f);
        }

        [Fact]
        public void NoiseGate_LetsTheVoiceThrough()
        {
            var settings = new MicProcessingSettings { Limiter = false, NoiseGate = true, GateThresholdDb = -45 };

            Assert.InRange(TailPeakDb(Run(settings, Tone(300, -20, 1.0)), 0.5), -20.5f, -19.5f);
        }

        [Fact]
        public void NoiseGate_ClosesAfterTheVoiceStops()
        {
            var settings = new MicProcessingSettings { Limiter = false, NoiseGate = true, GateThresholdDb = -45 };
            var input = Tone(300, -20, 0.5).Concat(Noise(-60, 1.5)).ToArray();

            Assert.True(TailPeakDb(Run(settings, input), 0.5) < -90f);
        }

        [Fact]
        public void Compressor_NarrowsTheGapBetweenLoudAndQuiet()
        {
            var settings = new MicProcessingSettings { Limiter = false, Compressor = true };

            var loud = TailPeakDb(Run(settings, Tone(500, -6, 1.0)), 0.5);
            var quiet = TailPeakDb(Run(settings, Tone(500, -30, 1.0)), 0.5);

            Assert.True(loud - quiet <= 14f, $"gap {loud - quiet:0.0} dB, was 24 dB");
            Assert.True(quiet > -30f, "makeup gain lifts quiet speech");
        }

        [Fact]
        public void Limiter_KeepsEveryPeakUnderMinusOneDbfs()
        {
            var settings = new MicProcessingSettings { Limiter = true, GainDb = 6 };
            var output = Run(settings, Tone(440, 0, 1.0));

            Assert.True(output.Select(Math.Abs).Max() <= MicProcessor.DbToLinear(-1f) + 1e-6f);
        }

        [Fact]
        public void Limiter_DoesNotTouchQuietSound()
        {
            var output = Run(new MicProcessingSettings(), Tone(440, -12, 0.5));

            Assert.InRange(TailPeakDb(output, 0.2), -12.05f, -11.95f);
        }

        [Fact]
        public void Reset_ClosesTheGateForANewSegment()
        {
            var settings = new MicProcessingSettings { Limiter = false, NoiseGate = true };
            var processor = new MicProcessor(settings, Rate);
            var voice = Tone(300, -20, 0.3);
            processor.Process(voice, voice.Length / 2);
            processor.Reset();

            var noise = Noise(-60, 0.05);
            processor.Process(noise, noise.Length / 2);

            Assert.True(noise.Select(Math.Abs).Max() < MicProcessor.DbToLinear(-70f));
        }

        [Fact]
        public void BrokenSamples_BecomeSilence_AndDoNotPoisonWhatFollows()
        {
            var settings = new MicProcessingSettings { HighPass = true, Compressor = true, Limiter = true, GainDb = 6 };
            var input = Tone(440, -12, 1.0);
            input[1000] = float.NaN;
            input[1001] = float.PositiveInfinity;
            input[2000] = float.NegativeInfinity;

            var output = Run(settings, input);

            Assert.All(output, sample => Assert.False(float.IsNaN(sample) || float.IsInfinity(sample)));
            Assert.True(TailPeakDb(output, 0.3) > -20f, "the tone keeps going after the glitch");
        }

        [Fact]
        public void Clamp_TurnsNaNIntoSilence()
        {
            var mix = new[] { float.NaN, 2f, -3f, 0.5f };
            AudioMixing.Clamp(mix, mix.Length);

            Assert.Equal(new[] { 0f, 1f, -1f, 0.5f }, mix);
        }

        [Fact]
        public void Settings_AreClampedAndDefaultsAreSafe()
        {
            var settings = new MicProcessingSettings { GainDb = 99, GateThresholdDb = float.NaN }.Normalize();

            Assert.Equal(MicProcessingSettings.MaxGainDb, settings.GainDb);
            Assert.Equal(-45f, settings.GateThresholdDb);
            Assert.True(new MicProcessingSettings().Limiter);
        }
    }
}

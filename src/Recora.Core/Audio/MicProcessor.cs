using System;
using System.Runtime.Serialization;

namespace Recora.Core.Audio
{
    /// <summary>The microphone chain the user sets on the Âm thanh page, like the filters of streaming software.</summary>
    [DataContract]
    public sealed class MicProcessingSettings
    {
        public const float MaxGainDb = 20f;
        public const float MinGateThresholdDb = -70f;
        public const float MaxGateThresholdDb = -20f;

        /// <summary>"Tăng âm": boost before everything else, for quiet headset microphones.</summary>
        [DataMember(Name = "gainDb", Order = 0)] public float GainDb { get; set; }

        /// <summary>"Lọc tiếng ù": cuts rumble, desk thumps and mains hum below about 80 Hz.</summary>
        [DataMember(Name = "highPass", Order = 1)] public bool HighPass { get; set; }

        /// <summary>"Chặn ồn nền": mutes the microphone between words (fans, keyboard, room noise).</summary>
        [DataMember(Name = "noiseGate", Order = 2)] public bool NoiseGate { get; set; }

        [DataMember(Name = "gateThresholdDb", Order = 3)] public float GateThresholdDb { get; set; } = -45f;

        /// <summary>"Nén tiếng": evens out whispering and shouting.</summary>
        [DataMember(Name = "compressor", Order = 4)] public bool Compressor { get; set; }

        /// <summary>"Chống rè": keeps peaks under -1 dBFS so a shout never clips into a crackle.</summary>
        [DataMember(Name = "limiter", Order = 5)] public bool Limiter { get; set; } = true;

        public MicProcessingSettings Normalize()
        {
            GainDb = Clamp(GainDb, 0f, MaxGainDb, 0f);
            GateThresholdDb = Clamp(GateThresholdDb, MinGateThresholdDb, MaxGateThresholdDb, -45f);
            return this;
        }

        public MicProcessingSettings Copy() => (MicProcessingSettings)MemberwiseClone();

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            GateThresholdDb = -45f;
            Limiter = true;
        }

        private static float Clamp(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));
    }

    /// <summary>
    /// Processes interleaved stereo float frames in place, block after block, in the order gain → high-pass → gate →
    /// compressor → limiter. Detectors look at both channels together so the stereo image never shifts. State carries
    /// across blocks; <see cref="Reset"/> clears it when a new segment starts.
    /// </summary>
    public sealed class MicProcessor
    {
        private const float CeilingDb = -1f;
        private const float CompressorThresholdDb = -24f;
        private const float CompressorRatio = 3f;
        private const float CompressorMakeupDb = 6f;

        private readonly MicProcessingSettings _settings;
        private readonly float _gain;
        private readonly Biquad _highPassLeft;
        private readonly Biquad _highPassRight;
        private readonly float _gateOpen;
        private readonly float _gateClose;
        private readonly float _gateHoldFrames;
        private readonly float _envelopeAttack;
        private readonly float _envelopeRelease;
        private readonly float _gateOpening;
        private readonly float _gateClosing;
        private readonly float _compAttack;
        private readonly float _compRelease;
        private readonly float _limiterRelease;
        private readonly float _ceiling;

        private float _envelope;
        private float _gateGain;
        private float _gateHeld;
        private float _compReductionDb;
        private float _limiterGain = 1f;

        public MicProcessor(MicProcessingSettings settings, int sampleRate)
        {
            _settings = settings.Copy().Normalize();
            _gain = DbToLinear(_settings.GainDb);
            _highPassLeft = Biquad.HighPass(80, 0.707, sampleRate);
            _highPassRight = Biquad.HighPass(80, 0.707, sampleRate);
            _gateOpen = DbToLinear(_settings.GateThresholdDb);
            _gateClose = DbToLinear(_settings.GateThresholdDb - 3f);
            _gateHoldFrames = 0.15f * sampleRate;
            _envelopeAttack = Coefficient(0.001, sampleRate);
            _envelopeRelease = Coefficient(0.100, sampleRate);
            _gateOpening = Coefficient(0.002, sampleRate);
            _gateClosing = Coefficient(0.120, sampleRate);
            _compAttack = Coefficient(0.005, sampleRate);
            _compRelease = Coefficient(0.120, sampleRate);
            _limiterRelease = Coefficient(0.050, sampleRate);
            _ceiling = DbToLinear(CeilingDb);
            Reset();
        }

        /// <summary>True when the chain changes nothing, so the pump can skip it.</summary>
        public bool IsBypass =>
            _settings.GainDb == 0f && !_settings.HighPass && !_settings.NoiseGate && !_settings.Compressor && !_settings.Limiter;

        public void Reset()
        {
            _highPassLeft.Reset();
            _highPassRight.Reset();
            _envelope = 0f;
            _gateGain = _settings.NoiseGate ? 0f : 1f;
            _gateHeld = 0f;
            _compReductionDb = 0f;
            _limiterGain = 1f;
        }

        public void Process(float[] stereo, int frames)
        {
            if (IsBypass)
            {
                return;
            }

            for (var f = 0; f < frames; f++)
            {
                // A driver glitch can hand over NaN or infinity; one such sample would poison every filter state
                // and make the AAC encoder refuse the rest of the segment, so it becomes silence here.
                var left = Finite(stereo[f * 2]) * _gain;
                var right = Finite(stereo[f * 2 + 1]) * _gain;

                if (_settings.HighPass)
                {
                    left = _highPassLeft.Next(left);
                    right = _highPassRight.Next(right);
                }

                var peak = Math.Max(Math.Abs(left), Math.Abs(right));

                if (_settings.NoiseGate)
                {
                    _envelope += (peak - _envelope) * (peak > _envelope ? _envelopeAttack : _envelopeRelease);
                    if (_envelope >= _gateOpen)
                    {
                        _gateHeld = _gateHoldFrames;
                    }
                    else if (_envelope < _gateClose && _gateHeld > 0)
                    {
                        _gateHeld--;
                    }

                    var target = _gateHeld > 0 ? 1f : 0f;
                    _gateGain += (target - _gateGain) * (target > _gateGain ? _gateOpening : _gateClosing);
                    left *= _gateGain;
                    right *= _gateGain;
                    peak *= _gateGain;
                }

                if (_settings.Compressor)
                {
                    var levelDb = LinearToDb(peak);
                    var wanted = levelDb > CompressorThresholdDb ? (levelDb - CompressorThresholdDb) * (1f - 1f / CompressorRatio) : 0f;
                    _compReductionDb += (wanted - _compReductionDb) * (wanted > _compReductionDb ? _compAttack : _compRelease);
                    var gain = DbToLinear(CompressorMakeupDb - _compReductionDb);
                    left *= gain;
                    right *= gain;
                    peak *= gain;
                }

                if (_settings.Limiter)
                {
                    // Instant attack: the gain drops on the very sample that would cross the ceiling, then recovers.
                    var needed = peak > _ceiling ? _ceiling / peak : 1f;
                    _limiterGain = needed < _limiterGain ? needed : _limiterGain + (1f - _limiterGain) * _limiterRelease;
                    if (_limiterGain > needed)
                    {
                        _limiterGain = needed;
                    }

                    left *= _limiterGain;
                    right *= _limiterGain;
                }

                stereo[f * 2] = left;
                stereo[f * 2 + 1] = right;
            }
        }

        private static float Finite(float sample) => float.IsNaN(sample) || float.IsInfinity(sample) ? 0f : sample;

        public static float DbToLinear(float db) => (float)Math.Pow(10, db / 20.0);

        public static float LinearToDb(float linear) => linear <= 1e-9f ? -180f : (float)(20 * Math.Log10(linear));

        /// <summary>One-pole smoothing factor reaching about 63 % of a step in <paramref name="seconds"/>.</summary>
        private static float Coefficient(double seconds, int sampleRate) => (float)(1 - Math.Exp(-1.0 / (seconds * sampleRate)));

        /// <summary>RBJ cookbook biquad, direct form I.</summary>
        private sealed class Biquad
        {
            private readonly float _b0, _b1, _b2, _a1, _a2;
            private float _x1, _x2, _y1, _y2;

            private Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
            {
                _b0 = (float)(b0 / a0);
                _b1 = (float)(b1 / a0);
                _b2 = (float)(b2 / a0);
                _a1 = (float)(a1 / a0);
                _a2 = (float)(a2 / a0);
            }

            public static Biquad HighPass(double frequency, double q, int sampleRate)
            {
                var w0 = 2 * Math.PI * frequency / sampleRate;
                var cos = Math.Cos(w0);
                var alpha = Math.Sin(w0) / (2 * q);
                return new Biquad((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
            }

            public float Next(float x)
            {
                var y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1;
                _x1 = x;
                _y2 = _y1;
                _y1 = y;
                return y;
            }

            public void Reset() => _x1 = _x2 = _y1 = _y2 = 0f;
        }
    }
}

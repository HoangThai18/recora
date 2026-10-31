using System;
using NAudio.CoreAudioApi;
using NAudio.Dmo;
using NAudio.Dsp;
using NAudio.Wave;
using Recora.App.Logging;
using Recora.Core.Audio;
using Recora.Core.Ffmpeg;

namespace Recora.App.Audio
{
    /// <summary>
    /// Converts whatever a WASAPI device delivers (float or integer PCM, any channel count, any rate) into the 48 kHz
    /// stereo floats the pump writes. Runs on NAudio's capture thread; buffers grow once and are reused.
    /// </summary>
    internal sealed class SampleConverter
    {
        private readonly WaveFormat _format;
        private readonly bool _isFloat;
        private readonly int _bytesPerSample;
        private readonly WdlResampler? _resampler;
        private float[] _interleaved = new float[0];
        private float[] _stereo = new float[0];
        private float[] _resampled = new float[0];

        public SampleConverter(WaveFormat format)
        {
            _format = format;
            _bytesPerSample = format.BitsPerSample / 8;
            _isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat
                || (format is WaveFormatExtensible extensible && extensible.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT);

            if (format.SampleRate != RecordingCommand.AudioSampleRate)
            {
                _resampler = new WdlResampler();
                _resampler.SetMode(true, 2, false);
                _resampler.SetFilterParms();
                _resampler.SetFeedMode(true);
                _resampler.SetRates(format.SampleRate, RecordingCommand.AudioSampleRate);
            }
        }

        /// <summary>Returns the number of 48 kHz stereo frames written to <paramref name="output"/>.</summary>
        public int Convert(byte[] buffer, int bytes, out float[] output)
        {
            var channels = _format.Channels;
            var frames = bytes / (_bytesPerSample * channels);
            Ensure(ref _interleaved, frames * channels);
            Ensure(ref _stereo, frames * 2);

            for (var i = 0; i < frames * channels; i++)
            {
                _interleaved[i] = ReadSample(buffer, i * _bytesPerSample);
            }

            AudioMixing.ToStereo(_interleaved, channels, _stereo, frames);

            if (_resampler == null)
            {
                output = _stereo;
                return frames;
            }

            var wanted = _resampler.ResamplePrepare(frames, 2, out var inBuffer, out var inOffset);
            Array.Copy(_stereo, 0, inBuffer, inOffset, wanted * 2);
            var maxOut = (int)Math.Ceiling(frames * (double)RecordingCommand.AudioSampleRate / _format.SampleRate) + 16;
            Ensure(ref _resampled, maxOut * 2);
            var produced = _resampler.ResampleOut(_resampled, 0, wanted, maxOut, 2);
            output = _resampled;
            return produced;
        }

        private float ReadSample(byte[] buffer, int offset)
        {
            if (_isFloat)
            {
                return BitConverter.ToSingle(buffer, offset);
            }

            return _bytesPerSample switch
            {
                2 => BitConverter.ToInt16(buffer, offset) / 32768f,
                3 => ((buffer[offset] << 8) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 24)) / 2147483648f,
                4 => BitConverter.ToInt32(buffer, offset) / 2147483648f,
                _ => 0f,
            };
        }

        private static void Ensure(ref float[] array, int length)
        {
            if (array.Length < length)
            {
                array = new float[length];
            }
        }
    }

    /// <summary>
    /// One WASAPI endpoint feeding a ring buffer for the whole session: loopback of a speaker for system sound, or a
    /// microphone. If the device disappears mid-recording, its input simply turns silent (DOMAIN section 6).
    /// </summary>
    internal sealed class WasapiSource : IDisposable
    {
        private readonly WasapiCapture _capture;
        private readonly SampleConverter _converter;
        private readonly AudioRingBuffer _buffer = new AudioRingBuffer(RecordingCommand.AudioSampleRate * 2);
        private volatile bool _disposing;

        private WasapiSource(WasapiCapture capture, string name)
        {
            _capture = capture;
            Name = name;
            _converter = new SampleConverter(capture.WaveFormat);
            Input = new RingBufferInput(_buffer);
            _capture.DataAvailable += OnData;
            _capture.RecordingStopped += OnStopped;
        }

        public event Action<WasapiSource>? Lost;

        public string Name { get; }

        public RingBufferInput Input { get; }

        public static WasapiSource Loopback(MMDevice device) => new WasapiSource(new WasapiLoopbackCapture(device), device.FriendlyName);

        public static WasapiSource Microphone(MMDevice device) => new WasapiSource(new WasapiCapture(device, true, 20), device.FriendlyName);

        public void Start()
        {
            Log.Info($"audio capture start: {Name} ({_capture.WaveFormat})");
            _capture.StartRecording();
        }

        public void Dispose()
        {
            _disposing = true;
            try
            {
                _capture.StopRecording();
            }
            catch (InvalidOperationException)
            {
            }

            _capture.Dispose();
        }

        private void OnData(object? sender, WaveInEventArgs e)
        {
            var frames = _converter.Convert(e.Buffer, e.BytesRecorded, out var stereo);
            if (frames > 0)
            {
                _buffer.Write(stereo, 0, frames);
            }
        }

        private void OnStopped(object? sender, StoppedEventArgs e)
        {
            if (!_disposing && e.Exception != null)
            {
                Log.Error($"audio device lost: {Name}", e.Exception);
                Lost?.Invoke(this);
            }
        }
    }
}

using System;

namespace Recora.Core.Audio
{
    /// <summary>
    /// Interleaved float samples between a capture callback (writer) and the pump thread (reader). Reads never block:
    /// missing samples come back as silence, so the recording timeline keeps running when a device goes quiet.
    /// The buffer also bounds latency: when the writer runs ahead (device clock faster than ours), the oldest samples
    /// are dropped instead of letting sound drift away from the picture.
    /// </summary>
    public sealed class AudioRingBuffer
    {
        private readonly object _gate = new object();
        private readonly float[] _samples;
        private readonly int _channels;
        private int _readFrame;
        private int _frames;

        public AudioRingBuffer(int capacityFrames, int channels = 2)
        {
            if (capacityFrames <= 0 || channels <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacityFrames));
            }

            _channels = channels;
            _samples = new float[capacityFrames * channels];
        }

        public int CapacityFrames => _samples.Length / _channels;

        public int AvailableFrames
        {
            get
            {
                lock (_gate)
                {
                    return _frames;
                }
            }
        }

        /// <summary>Total frames ever dropped because the buffer was full or over the latency limit.</summary>
        public long DroppedFrames { get; private set; }

        public void Write(float[] source, int offsetFrames, int frames)
        {
            lock (_gate)
            {
                // Writing more than fits keeps only the newest part: the oldest audio is the least useful.
                if (frames > CapacityFrames)
                {
                    DroppedFrames += frames - CapacityFrames;
                    offsetFrames += frames - CapacityFrames;
                    frames = CapacityFrames;
                }

                var overflow = _frames + frames - CapacityFrames;
                if (overflow > 0)
                {
                    Skip(overflow);
                }

                var writeFrame = (_readFrame + _frames) % CapacityFrames;
                CopyIn(source, offsetFrames, writeFrame, frames);
                _frames += frames;
            }
        }

        /// <summary>Copies up to <paramref name="frames"/> frames and pads the rest with silence. Returns frames that were real audio.</summary>
        public int Read(float[] destination, int offsetFrames, int frames)
        {
            lock (_gate)
            {
                var real = Math.Min(frames, _frames);
                CopyOut(destination, offsetFrames, real);
                _readFrame = (_readFrame + real) % CapacityFrames;
                _frames -= real;
                Array.Clear(destination, (offsetFrames + real) * _channels, (frames - real) * _channels);
                return real;
            }
        }

        /// <summary>Drops the oldest frames beyond <paramref name="maxFrames"/>, keeping latency bounded.</summary>
        public void TrimTo(int maxFrames)
        {
            lock (_gate)
            {
                if (_frames > maxFrames)
                {
                    Skip(_frames - maxFrames);
                }
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _readFrame = 0;
                _frames = 0;
            }
        }

        private void Skip(int frames)
        {
            _readFrame = (_readFrame + frames) % CapacityFrames;
            _frames -= frames;
            DroppedFrames += frames;
        }

        private void CopyIn(float[] source, int sourceFrame, int ringFrame, int frames)
        {
            var first = Math.Min(frames, CapacityFrames - ringFrame);
            Array.Copy(source, sourceFrame * _channels, _samples, ringFrame * _channels, first * _channels);
            Array.Copy(source, (sourceFrame + first) * _channels, _samples, 0, (frames - first) * _channels);
        }

        private void CopyOut(float[] destination, int destinationFrame, int frames)
        {
            var first = Math.Min(frames, CapacityFrames - _readFrame);
            Array.Copy(_samples, _readFrame * _channels, destination, destinationFrame * _channels, first * _channels);
            Array.Copy(_samples, 0, destination, (destinationFrame + first) * _channels, (frames - first) * _channels);
        }
    }
}

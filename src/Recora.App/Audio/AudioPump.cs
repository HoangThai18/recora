using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using Recora.App.Logging;
using Recora.Core.Audio;
using Recora.Core.Ffmpeg;

namespace Recora.App.Audio
{
    /// <summary>A sound source the pump pulls from: fills interleaved 48 kHz stereo floats, silence when it has nothing.</summary>
    internal interface IAudioInput
    {
        void Read(float[] stereo, int frames);

        /// <summary>Called when FFmpeg connects: audio captured before the segment started must not leak into it.</summary>
        void Reset();
    }

    internal sealed class RingBufferInput : IAudioInput
    {
        // ~60 ms: covers WASAPI's 10-20 ms packets plus timer jitter without letting sound lag the picture.
        private const int MaxLatencyFrames = RecordingCommand.AudioSampleRate * 60 / 1000;

        public RingBufferInput(AudioRingBuffer buffer) => Buffer = buffer;

        public AudioRingBuffer Buffer { get; }

        public void Read(float[] stereo, int frames)
        {
            Buffer.TrimTo(MaxLatencyFrames + frames);
            Buffer.Read(stereo, 0, frames);
        }

        public void Reset() => Buffer.Clear();
    }

    /// <summary>A quiet sine tone: proves in tests that audio really flows end to end.</summary>
    internal sealed class ToneInput : IAudioInput
    {
        private readonly double _step;
        private readonly float _amplitude;
        private double _phase;

        public ToneInput(double frequency, float amplitude)
        {
            _step = 2 * Math.PI * frequency / RecordingCommand.AudioSampleRate;
            _amplitude = amplitude;
        }

        public void Read(float[] stereo, int frames)
        {
            for (var f = 0; f < frames; f++)
            {
                var sample = (float)(Math.Sin(_phase) * _amplitude);
                stereo[f * 2] = sample;
                stereo[f * 2 + 1] = sample;
                _phase += _step;
            }

            _phase %= 2 * Math.PI;
        }

        public void Reset()
        {
        }
    }

    internal sealed record AudioTrackSpec(string Name, IReadOnlyList<(IAudioInput Input, float Gain)> Inputs);

    /// <summary>
    /// Writes each audio track into its own named pipe that FFmpeg reads as raw f32le. The amount written follows the
    /// app's clock (<see cref="AudioPacer"/>), not the devices': WASAPI loopback goes silent without sending packets,
    /// and the gap must still become silence or the sound would end up shorter than the video.
    /// </summary>
    internal sealed class AudioPump : IDisposable
    {
        private const int ChunkFrames = RecordingCommand.AudioSampleRate / 10;
        private readonly List<Worker> _workers;
        private volatile bool _stopping;

        public AudioPump(IReadOnlyList<AudioTrackSpec> tracks)
        {
            var session = Guid.NewGuid().ToString("N");
            _workers = tracks.Select((track, i) => new Worker(this, track, $"recora-{session}-{i}")).ToList();
        }

        public event Action<string, Exception>? Failed;

        /// <summary>Paths for FFmpeg's <c>-i</c>, in track order.</summary>
        public IReadOnlyList<string> PipePaths => _workers.Select(w => @"\\.\pipe\" + w.PipeName).ToList();

        public void Start()
        {
            foreach (var worker in _workers)
            {
                worker.Start();
            }
        }

        /// <summary>
        /// Stops writing without closing the pipes. Call it right before asking FFmpeg to quit, so the pipe it closes on
        /// exit is not reported as a failure.
        /// </summary>
        public void BeginStop() => _stopping = true;

        public void Stop()
        {
            _stopping = true;
            foreach (var worker in _workers)
            {
                worker.Stop();
            }
        }

        public void Dispose() => Stop();

        private sealed class Worker
        {
            private readonly AudioPump _owner;
            private readonly AudioTrackSpec _track;
            private readonly NamedPipeServerStream _pipe;
            private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
            private readonly Thread _thread;

            public Worker(AudioPump owner, AudioTrackSpec track, string pipeName)
            {
                _owner = owner;
                _track = track;
                PipeName = pipeName;
                _pipe = CreatePipe(pipeName);
                _thread = new Thread(Run) { IsBackground = true, Name = "AudioPump " + track.Name, Priority = ThreadPriority.AboveNormal };
            }

            public string PipeName { get; }

            public void Start() => _thread.Start();

            public void Stop()
            {
                // Cancelling releases a thread still waiting for FFmpeg to connect; disposing ends a blocked Write.
                _cancel.Cancel();
                _pipe.Dispose();
                if (_thread.IsAlive && !_thread.Join(TimeSpan.FromSeconds(3)))
                {
                    Log.Warn($"audio pump {_track.Name} did not stop in time");
                }
            }

            /// <summary>Only the current user may connect: the pipe carries what the microphone hears.</summary>
            private static NamedPipeServerStream CreatePipe(string name)
            {
                var security = new PipeSecurity();
                security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.FullControl, AccessControlType.Allow));
                return new NamedPipeServerStream(
                    name, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 1 << 20, security);
            }

            private void Run()
            {
                try
                {
                    _pipe.WaitForConnectionAsync(_cancel.Token).GetAwaiter().GetResult();
                    foreach (var (input, _) in _track.Inputs)
                    {
                        input.Reset();
                    }

                    Pump();
                }
                catch (Exception error) when (_owner._stopping && (error is IOException || error is ObjectDisposedException || error is OperationCanceledException))
                {
                    // Expected: Stop() closed the pipe, or FFmpeg exited first after its 'q'.
                }
                catch (Exception error)
                {
                    Log.Error($"audio pump {_track.Name} failed", error);
                    _owner.Failed?.Invoke(_track.Name, error);
                }
            }

            private void Pump()
            {
                var pacer = new AudioPacer(RecordingCommand.AudioSampleRate, ChunkFrames);
                var clock = Stopwatch.StartNew();
                var mix = new float[ChunkFrames * 2];
                var scratch = new float[ChunkFrames * 2];
                var bytes = new byte[ChunkFrames * 2 * sizeof(float)];

                while (!_owner._stopping)
                {
                    var frames = pacer.Due(clock.Elapsed);
                    if (frames == 0)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    var samples = frames * 2;
                    Array.Clear(mix, 0, samples);
                    foreach (var (input, gain) in _track.Inputs)
                    {
                        input.Read(scratch, frames);
                        AudioMixing.Accumulate(mix, scratch, gain, samples);
                    }

                    AudioMixing.Clamp(mix, samples);
                    Buffer.BlockCopy(mix, 0, bytes, 0, samples * sizeof(float));
                    _pipe.Write(bytes, 0, samples * sizeof(float));
                    pacer.Sent(frames);
                }
            }
        }
    }
}

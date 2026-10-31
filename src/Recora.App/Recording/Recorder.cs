using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Recora.App.Audio;
using Recora.App.Logging;
using Recora.Core;
using Recora.Core.Capture;
using Recora.Core.Encoding;
using Recora.Core.Ffmpeg;
using Recora.Core.Recording;

namespace Recora.App.Recording
{
    /// <summary>What one recording should capture, resolved from settings when the user presses record.</summary>
    internal sealed record RecordingRequest(
        CaptureSource Source,
        VideoCodec Codec,
        EncoderKind? PreferredEncoder,
        QualityPreset Quality,
        int Fps,
        OutputScale Scale,
        bool DrawCursor,
        ContainerFormat Container,
        string OutputFolder,
        IReadOnlyList<AudioTrackSpec> AudioTracks,
        AudioFormat? AudioOnly = null);

    internal sealed record SavedRecording(string Path, TimeSpan Duration, long Bytes);

    internal sealed record RecorderStatus(SessionState State, TimeSpan Elapsed, long Bytes, double Fps, string? Encoder, string? OutputPath, string? ErrorCode);

    /// <summary>
    /// Runs one recording session at a time: segments with pause/resume, encoder fallback at each start (BR-02),
    /// finalizing by stream copy, and the session folder + manifest that make a crash recoverable (BR-01).
    /// All public methods are called from the UI thread; blocking work runs on the thread pool.
    /// </summary>
    internal sealed class Recorder
    {
        private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(6);
        private readonly CaptureEnvironment _environment;
        private readonly EncoderMemory _memory;
        private readonly SessionStateMachine _state = new SessionStateMachine();
        private readonly object _gate = new object();
        private Session? _session;

        public Recorder(CaptureEnvironment environment, EncoderMemory memory)
        {
            _environment = environment;
            _memory = memory;
        }

        public event Action<RecorderStatus>? Changed;

        public SessionState State
        {
            get
            {
                lock (_gate)
                {
                    return _state.State;
                }
            }
        }

        public async Task StartAsync(RecordingRequest request)
        {
            lock (_gate)
            {
                if (!_state.CanStart)
                {
                    return;
                }

                _state.MoveTo(SessionState.Starting);
            }

            Raise(null);

            try
            {
                var session = await Task.Run(() => Session.Create(request)).ConfigureAwait(false);
                _session = session;
                await Task.Run(() => StartSegment(session)).ConfigureAwait(false);
                Move(SessionState.Recording);
            }
            catch (Exception error)
            {
                Log.Error("recording failed to start", error);
                await Task.Run(() => Abandon()).ConfigureAwait(false);
                Move(SessionState.Failed, ErrorCode(error));
            }
        }

        public async Task PauseAsync()
        {
            var session = _session;
            lock (_gate)
            {
                if (!_state.CanPause || session == null)
                {
                    return;
                }

                _state.MoveTo(SessionState.Paused);
            }

            await Task.Run(() => session.CloseSegment()).ConfigureAwait(false);
            Raise(null);
        }

        public async Task ResumeAsync()
        {
            var session = _session;
            lock (_gate)
            {
                if (!_state.CanResume || session == null)
                {
                    return;
                }

                _state.MoveTo(SessionState.Starting);
            }

            Raise(null);
            try
            {
                await Task.Run(() => StartSegment(session)).ConfigureAwait(false);
                Move(SessionState.Recording);
            }
            catch (Exception error)
            {
                Log.Error("recording failed to resume", error);
                await StopAsync().ConfigureAwait(false);
            }
        }

        /// <summary>Stops and writes the final file. Returns it, or null if nothing was recorded or saving failed.</summary>
        public async Task<SavedRecording?> StopAsync()
        {
            var session = _session;
            lock (_gate)
            {
                if (!_state.CanStop || session == null)
                {
                    return null;
                }

                _state.MoveTo(SessionState.Finalizing);
            }

            Raise(null);

            try
            {
                var saved = await Task.Run(() =>
                {
                    session.CloseSegment();
                    session.StopAudio();
                    var output = Finalizer.Finish(_environment.FfmpegPath, session.Folder, session.Manifest, session.Segments);
                    if (output == null)
                    {
                        return null;
                    }

                    LibraryFiles.Remember(output, session.Elapsed);
                    return new SavedRecording(output, session.Elapsed, new FileInfo(output).Length);
                }).ConfigureAwait(false);

                _session = null;
                Move(SessionState.Idle, outputPath: saved?.Path);
                return saved;
            }
            catch (Exception error)
            {
                Log.Error("finalizing failed; segments kept in " + session.Folder, error);
                _session = null;
                Move(SessionState.Failed, ErrorCode(error));
                return null;
            }
        }

        /// <summary>Snapshot for the status bar, polled by the UI timer.</summary>
        public RecorderStatus Status()
        {
            var session = _session;
            return new RecorderStatus(State, session?.Elapsed ?? TimeSpan.Zero, session?.Bytes ?? 0, session?.CurrentFps ?? 0, session?.EncoderName, null, null);
        }

        private void StartSegment(Session session)
        {
            var request = session.Request;
            if (request.AudioOnly is AudioFormat format)
            {
                var failure = session.TryOpenAudioSegment(_environment.FfmpegPath, format, request.Quality, FirstFrameTimeout);
                if (failure == null)
                {
                    return;
                }

                Log.Warn($"audio recording ({format}) failed: {failure}");
                throw new RecoraException("audio_failed", $"{format}: {failure}");
            }

            var backends = Backends(request.Source);
            var encoders = _memory.Order(
                EncoderCatalog.Candidates(request.Source.Monitor.Vendor, request.Codec, _environment.Encoders),
                request.PreferredEncoder);
            var failures = new List<string>();

            foreach (var backend in backends)
            {
                foreach (var encoder in encoders)
                {
                    var plan = new RecordingPlan(request.Source, backend, encoder, request.Quality, request.Fps, request.Scale, request.DrawCursor, Layout(request));
                    var outcome = session.TryOpenSegment(_environment.FfmpegPath, plan, FirstFrameTimeout);

                    if (outcome == null)
                    {
                        _memory.Worked(encoder);
                        return;
                    }

                    failures.Add($"{backend}/{encoder.FfmpegName}: {outcome}");
                    Log.Warn($"candidate {backend}/{encoder.FfmpegName} failed: {outcome}");

                    // A capture that cannot even open fails the same way for every encoder: skip to the next backend,
                    // and don't hold it against the encoder.
                    if (outcome.IndexOf("Error opening input", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        break;
                    }

                    _memory.Failed(encoder);
                }
            }

            if (request.Source is DeviceSource && failures.All(f => f.IndexOf("Error opening input", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                throw new RecoraException("device_unavailable", "the device did not open: " + string.Join(" || ", failures));
            }

            throw new RecoraException("no_working_encoder", "no capture/encoder pair worked: " + string.Join(" || ", failures));
        }

        private IEnumerable<CaptureBackend> Backends(CaptureSource source)
        {
            var first = CaptureBackendSelector.Select(source, _environment.Capabilities);
            yield return first;
            if (first == CaptureBackend.Dshow)
            {
                yield break;
            }

            if (first != CaptureBackend.Gfxcapture && _environment.Capabilities.HasGfxcapture && source is not RegionSource)
            {
                yield return CaptureBackend.Gfxcapture;
            }

            if (first != CaptureBackend.Gdigrab)
            {
                yield return CaptureBackend.Gdigrab;
            }
        }

        private static AudioLayout Layout(RecordingRequest request) => request.AudioTracks.Count switch
        {
            0 => AudioLayout.None,
            1 => AudioLayout.Mixed,
            _ => AudioLayout.SeparateTracks,
        };

        private void Abandon()
        {
            var session = _session;
            _session = null;
            if (session == null)
            {
                return;
            }

            session.CloseSegment();
            session.StopAudio();
            if (session.Segments.Count == 0)
            {
                session.DeleteFolder();
            }
        }

        private void Move(SessionState next, string? errorCode = null, string? outputPath = null)
        {
            lock (_gate)
            {
                if (_state.State != next)
                {
                    _state.MoveTo(next);
                }
            }

            Raise(errorCode, outputPath);
        }

        private void Raise(string? errorCode, string? outputPath = null)
        {
            var status = Status();
            Changed?.Invoke(status with { ErrorCode = errorCode, OutputPath = outputPath });
        }

        private static string ErrorCode(Exception error) => error is RecoraException known ? known.Code : "unexpected";
    }
}

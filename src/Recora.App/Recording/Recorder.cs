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
        AudioFormat? AudioOnly = null,
        int MaxGigabytesPerHour = 0,
        bool LightOnCpu = false);

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
        private static readonly TimeSpan WatchInterval = TimeSpan.FromSeconds(1);
        private const int RestartAttempts = 6;
        private readonly CaptureEnvironment _environment;
        private readonly object _segmentLock = new object();
        private readonly System.Threading.Timer _watchdog;
        private int _healing;
        private readonly EncoderMemory _memory;
        private readonly SessionStateMachine _state = new SessionStateMachine();
        private readonly object _gate = new object();
        private Session? _session;

        public Recorder(CaptureEnvironment environment, EncoderMemory memory)
        {
            _environment = environment;
            _memory = memory;
            _watchdog = new System.Threading.Timer(Watch, null, WatchInterval, WatchInterval);
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

            await Task.Run(() =>
            {
                lock (_segmentLock)
                {
                    session.CloseSegment();
                }
            }).ConfigureAwait(false);
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
                await Task.Run(() =>
                {
                    lock (_segmentLock)
                    {
                        StartSegment(session);
                    }
                }).ConfigureAwait(false);
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
                    lock (_segmentLock)
                    {
                        session.CloseSegment();
                    }

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

        /// <summary>
        /// A live FFmpeg that ended by itself (a game took the screen into exclusive fullscreen) is replaced by a new
        /// segment: first the same capture a few times while Windows finishes switching (falling back to GDI would
        /// only record black), then any capture. The recording goes on; the gap is about a second.
        /// </summary>
        private void Watch(object? state)
        {
            var session = _session;
            if (session == null || Interlocked.CompareExchange(ref _healing, 1, 0) != 0)
            {
                return;
            }

            try
            {
                lock (_segmentLock)
                {
                    if (State != SessionState.Recording || !ReferenceEquals(session, _session))
                    {
                        return;
                    }

                    var reason = session.LiveSegmentEnded();
                    if (reason == null)
                    {
                        return;
                    }

                    var backend = session.LivePlan?.Backend;
                    Log.Warn($"ffmpeg stopped mid-recording ({backend}): {reason}");
                    session.CloseSegment();
                    for (var attempt = 1; attempt <= RestartAttempts; attempt++)
                    {
                        try
                        {
                            StartSegment(session, attempt < RestartAttempts ? backend : null);
                            Log.Info($"recording continues in a new segment (attempt {attempt})");
                            Raise(null);
                            return;
                        }
                        catch (RecoraException error)
                        {
                            Log.Warn($"restart attempt {attempt} failed: {error.Message}");
                            Thread.Sleep(1000);
                        }
                    }

                    Log.Error("could not restart the capture; saving what was recorded", null);
                }

                StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception error) when (error is IOException || error is InvalidOperationException || error is UnauthorizedAccessException || error is System.ComponentModel.Win32Exception)
            {
                // A timer thread must never take the app down; the recording keeps its segments on disk (BR-01).
                Log.Error("restarting the capture failed", error);
            }
            finally
            {
                Interlocked.Exchange(ref _healing, 0);
            }
        }

        /// <summary>For the selftest: kills the live FFmpeg the way Windows does when a game goes fullscreen.</summary>
        internal void KillLiveForTest() => _session?.KillLiveForTest();

        private void StartSegment(Session session) => StartSegment(session, null);

        private void StartSegment(Session session, CaptureBackend? only)
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

            // Segments are joined without re-encoding, so a resume keeps the first segment's size and rate even if another
            // encoder takes over; a size change inside the file would break the video.
            if (session.LivePlan is RecordingPlan live)
            {
                request = request with { Fps = live.Fps, Scale = live.Scale, LightOnCpu = false };
            }

            CandidateRunner.Run(_environment, _memory, request, plan => session.TryOpenSegment(_environment.FfmpegPath, plan, FirstFrameTimeout), only);
        }

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

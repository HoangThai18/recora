using System;

namespace Recora.Core.Recording
{
    public enum SessionState
    {
        Idle,
        Starting,
        Recording,
        Paused,
        Finalizing,
        Failed,
    }

    /// <summary>The recording lifecycle from docs/domain/DOMAIN.md section 4; any other move is a bug, not a user error.</summary>
    public sealed class SessionStateMachine
    {
        public SessionState State { get; private set; } = SessionState.Idle;

        public bool CanStart => State == SessionState.Idle || State == SessionState.Failed;

        public bool CanPause => State == SessionState.Recording;

        public bool CanResume => State == SessionState.Paused;

        public bool CanStop => State == SessionState.Recording || State == SessionState.Paused || State == SessionState.Starting;

        public void MoveTo(SessionState next)
        {
            if (!Allowed(State, next))
            {
                throw new InvalidOperationException($"recording cannot go from {State} to {next}");
            }

            State = next;
        }

        public static bool Allowed(SessionState from, SessionState to) => (from, to) switch
        {
            (SessionState.Idle, SessionState.Starting) => true,
            (SessionState.Failed, SessionState.Starting) => true,
            (SessionState.Failed, SessionState.Idle) => true,
            (SessionState.Starting, SessionState.Recording) => true,
            (SessionState.Starting, SessionState.Failed) => true,
            (SessionState.Starting, SessionState.Finalizing) => true,
            (SessionState.Recording, SessionState.Paused) => true,
            (SessionState.Recording, SessionState.Finalizing) => true,
            (SessionState.Recording, SessionState.Failed) => true,
            (SessionState.Paused, SessionState.Starting) => true,
            (SessionState.Paused, SessionState.Finalizing) => true,
            (SessionState.Finalizing, SessionState.Idle) => true,
            (SessionState.Finalizing, SessionState.Failed) => true,
            _ => false,
        };
    }
}

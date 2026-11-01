using System;

namespace Recora.Core.Animation
{
    public static class Easing
    {
        /// <summary>Fast start, gentle stop: what UI motion should feel like (Material's "decelerate").</summary>
        public static double OutCubic(double t)
        {
            var u = 1 - Clamp01(t);
            return 1 - u * u * u;
        }

        /// <summary>0 → 1 → 0 once per period, smooth at both ends: for breathing and blinking.</summary>
        public static double Pulse(TimeSpan elapsed, TimeSpan period)
        {
            if (period <= TimeSpan.Zero)
            {
                return 0;
            }

            var phase = (elapsed.Ticks % period.Ticks) / (double)period.Ticks;
            return (1 - Math.Cos(2 * Math.PI * phase)) / 2;
        }

        public static double Clamp01(double t) => t < 0 ? 0 : t > 1 ? 1 : t;
    }

    /// <summary>
    /// One animated number (a knob position, a hover glow, a colour mix). The caller owns the clock: it passes "now"
    /// to <see cref="To"/> and <see cref="Update"/>, so the motion is exact whatever the timer jitter, and testable.
    /// A new target mid-way starts from where the value is, never jumps back.
    /// </summary>
    public sealed class Tween
    {
        private double _from;
        private TimeSpan _startedAt;
        private TimeSpan _duration;

        public Tween(double value)
        {
            Value = value;
            Target = value;
            _from = value;
        }

        public double Value { get; private set; }

        public double Target { get; private set; }

        public bool IsRunning { get; private set; }

        public void To(double target, TimeSpan duration, TimeSpan now)
        {
            if (duration <= TimeSpan.Zero)
            {
                Jump(target);
                return;
            }

            if (target.Equals(Target) && (IsRunning || Value.Equals(target)))
            {
                return;
            }

            _from = Value;
            Target = target;
            _startedAt = now;
            _duration = duration;
            IsRunning = true;
        }

        public void Jump(double value)
        {
            Value = value;
            Target = value;
            _from = value;
            IsRunning = false;
        }

        /// <summary>Moves the value to where it should be at <paramref name="now"/>; false once it has arrived.</summary>
        public bool Update(TimeSpan now)
        {
            if (!IsRunning)
            {
                return false;
            }

            var t = (now - _startedAt).TotalMilliseconds / _duration.TotalMilliseconds;
            if (t >= 1)
            {
                Value = Target;
                IsRunning = false;
                return false;
            }

            Value = _from + (Target - _from) * Easing.OutCubic(t);
            return true;
        }
    }
}

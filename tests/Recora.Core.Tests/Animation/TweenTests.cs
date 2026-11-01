using System;
using Recora.Core.Animation;

namespace Recora.Core.Tests.Animation
{
    public class TweenTests
    {
        private static TimeSpan Ms(double value) => TimeSpan.FromMilliseconds(value);

        [Fact]
        public void To_MovesFromTheCurrentValueAndArrivesExactly()
        {
            var tween = new Tween(0);
            tween.To(1, Ms(200), Ms(1000));

            Assert.True(tween.Update(Ms(1100)));
            Assert.InRange(tween.Value, 0.8, 0.9);
            Assert.False(tween.Update(Ms(1200)));
            Assert.Equal(1, tween.Value);
            Assert.False(tween.IsRunning);
        }

        [Fact]
        public void OutCubic_StartsFastEndsSoftAndIsMonotonic()
        {
            Assert.Equal(0, Easing.OutCubic(0));
            Assert.Equal(1, Easing.OutCubic(1));
            Assert.True(Easing.OutCubic(0.25) > 0.5, "a quarter of the time covers more than half the way");
            var previous = 0.0;
            for (var t = 0.0; t <= 1.0001; t += 0.05)
            {
                var value = Easing.OutCubic(t);
                Assert.True(value >= previous);
                previous = value;
            }

            Assert.Equal(1, Easing.OutCubic(7));
            Assert.Equal(0, Easing.OutCubic(-3));
        }

        [Fact]
        public void To_MidWay_ReversesFromWhereItIsWithoutJumping()
        {
            var tween = new Tween(0);
            tween.To(1, Ms(200), Ms(0));
            tween.Update(Ms(50));
            var reached = tween.Value;

            tween.To(0, Ms(200), Ms(50));
            tween.Update(Ms(50));

            Assert.Equal(reached, tween.Value);
            tween.Update(Ms(60));
            Assert.True(tween.Value < reached);
        }

        [Fact]
        public void To_SameTargetAgain_DoesNotRestartTheClock()
        {
            var tween = new Tween(0);
            tween.To(1, Ms(100), Ms(0));
            tween.To(1, Ms(100), Ms(90));

            Assert.False(tween.Update(Ms(100)));
            Assert.Equal(1, tween.Value);
        }

        [Fact]
        public void Jump_AndZeroDuration_SetTheValueAtOnce()
        {
            var tween = new Tween(0);
            tween.To(5, TimeSpan.Zero, Ms(0));
            Assert.Equal(5, tween.Value);
            Assert.False(tween.IsRunning);

            tween.To(9, Ms(100), Ms(0));
            tween.Jump(2);
            Assert.Equal(2, tween.Value);
            Assert.False(tween.Update(Ms(50)));
        }

        [Fact]
        public void Pulse_BreathesOncePerPeriod()
        {
            var period = Ms(1600);

            Assert.Equal(0, Easing.Pulse(Ms(0), period), 6);
            Assert.Equal(1, Easing.Pulse(Ms(800), period), 6);
            Assert.Equal(0, Easing.Pulse(Ms(1600), period), 6);
            Assert.Equal(0.5, Easing.Pulse(Ms(400), period), 6);
            Assert.Equal(0, Easing.Pulse(Ms(100), TimeSpan.Zero));
        }
    }
}

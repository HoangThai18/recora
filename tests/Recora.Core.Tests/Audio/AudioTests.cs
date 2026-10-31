using Recora.Core.Audio;

namespace Recora.Core.Tests.Audio
{
    public class AudioRingBufferTests
    {
        private static float[] Frames(params float[] leftValues) =>
            leftValues.SelectMany(v => new[] { v, -v }).ToArray();

        [Fact]
        public void Read_WhenEmpty_ReturnsSilenceAndZeroRealFrames()
        {
            var buffer = new AudioRingBuffer(8);
            var output = Enumerable.Repeat(9f, 8).ToArray();

            var real = buffer.Read(output, 0, 4);

            Assert.Equal(0, real);
            Assert.All(output, sample => Assert.Equal(0f, sample));
        }

        [Fact]
        public void Read_WhenShort_CopiesWhatExistsThenPadsWithSilence()
        {
            var buffer = new AudioRingBuffer(8);
            buffer.Write(Frames(0.1f, 0.2f), 0, 2);
            var output = new float[8];

            var real = buffer.Read(output, 0, 4);

            Assert.Equal(2, real);
            Assert.Equal(new[] { 0.1f, -0.1f, 0.2f, -0.2f, 0f, 0f, 0f, 0f }, output);
        }

        [Fact]
        public void Write_WhenFull_DropsOldestFramesAndCountsThem()
        {
            var buffer = new AudioRingBuffer(4);
            buffer.Write(Frames(1, 2, 3), 0, 3);
            buffer.Write(Frames(4, 5, 6), 0, 3);
            var output = new float[8];

            buffer.Read(output, 0, 4);

            Assert.Equal(Frames(3, 4, 5, 6), output);
            Assert.Equal(2, buffer.DroppedFrames);
        }

        [Fact]
        public void Write_LargerThanCapacity_KeepsNewestPart()
        {
            var buffer = new AudioRingBuffer(2);
            buffer.Write(Frames(1, 2, 3, 4, 5), 0, 5);
            var output = new float[4];

            buffer.Read(output, 0, 2);

            Assert.Equal(Frames(4, 5), output);
        }

        [Fact]
        public void TrimTo_BoundsLatencyByDroppingOldest()
        {
            var buffer = new AudioRingBuffer(16);
            buffer.Write(Frames(1, 2, 3, 4, 5, 6), 0, 6);

            buffer.TrimTo(2);
            var output = new float[4];
            buffer.Read(output, 0, 2);

            Assert.Equal(Frames(5, 6), output);
            Assert.Equal(4, buffer.DroppedFrames);
        }

        [Fact]
        public void WrapAround_PreservesOrder()
        {
            var buffer = new AudioRingBuffer(4);
            var output = new float[6];
            buffer.Write(Frames(1, 2, 3), 0, 3);
            buffer.Read(output, 0, 2);
            buffer.Write(Frames(4, 5, 6), 0, 3);

            buffer.Read(output, 0, 3);

            Assert.Equal(Frames(3, 4, 5), output);
            Assert.Equal(1, buffer.AvailableFrames);
        }
    }

    public class AudioMixingTests
    {
        [Fact]
        public void AccumulateAndClamp_LimitsSumOfLoudSources()
        {
            var mix = new float[4];

            AudioMixing.Accumulate(mix, new[] { 0.8f, -0.8f, 0.2f, 0f }, 1f, 4);
            AudioMixing.Accumulate(mix, new[] { 0.8f, -0.8f, 0.2f, 0.5f }, 0.5f, 4);
            AudioMixing.Clamp(mix, 4);

            Assert.Equal(new[] { 1f, -1f, 0.3f, 0.25f }, mix);
        }

        [Fact]
        public void ToStereo_DuplicatesMonoAndKeepsFrontPairOfSurround()
        {
            var stereo = new float[4];

            AudioMixing.ToStereo(new[] { 0.5f, 0.25f }, 1, stereo, 2);
            Assert.Equal(new[] { 0.5f, 0.5f, 0.25f, 0.25f }, stereo);

            AudioMixing.ToStereo(new[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f }, 6, stereo, 2);
            Assert.Equal(new[] { 1f, 2f, 7f, 8f }, stereo);
        }
    }

    public class AudioPacerTests
    {
        [Fact]
        public void Due_FollowsElapsedTimeExactlyAcrossJitter()
        {
            var pacer = new AudioPacer(48000, maxChunkFrames: 100_000);
            var total = 0;

            foreach (var ms in new[] { 7, 23, 24, 61, 100, 1000 })
            {
                var due = pacer.Due(TimeSpan.FromMilliseconds(ms));
                pacer.Sent(due);
                total += due;
            }

            Assert.Equal(48_000, total);
            Assert.Equal(0, pacer.Due(TimeSpan.FromMilliseconds(1000)));
        }

        [Fact]
        public void Due_CapsOneCallButCatchesUpLater()
        {
            var pacer = new AudioPacer(48000, maxChunkFrames: 4800);

            var first = pacer.Due(TimeSpan.FromSeconds(1));
            pacer.Sent(first);

            Assert.Equal(4800, first);
            Assert.Equal(4800, pacer.Due(TimeSpan.FromSeconds(1)));
        }
    }
}

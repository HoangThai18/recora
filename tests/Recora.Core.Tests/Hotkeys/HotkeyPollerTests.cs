using System.Collections.Generic;
using System.Linq;
using Recora.Core.Hotkeys;
using Xunit;

namespace Recora.Core.Tests.Hotkeys
{
    public class HotkeyPollerTests
    {
        private const int F12 = 0x7B;

        private static HotkeyPoller<string> Poller()
        {
            var poller = new HotkeyPoller<string>();
            poller.Set(new Dictionary<string, Hotkey>
            {
                ["record"] = Hotkey.Record,
                ["pause"] = Hotkey.Pause,
            });
            return poller;
        }

        private static IReadOnlyList<string> Step(HotkeyPoller<string> poller, params int[] down) =>
            poller.Step(vk => down.Contains(vk));

        [Fact]
        public void F12_fires_once_while_held_and_again_after_release()
        {
            var poller = Poller();

            Assert.Equal(new[] { "record" }, Step(poller, F12));
            Assert.Empty(Step(poller, F12));
            Assert.Empty(Step(poller));
            Assert.Equal(new[] { "record" }, Step(poller, F12));
        }

        [Fact]
        public void Modifiers_must_match_exactly()
        {
            var poller = Poller();

            Assert.Equal(new[] { "pause" }, Step(poller, HotkeyPoller<string>.VkShift, F12));
            Assert.Empty(Step(poller, HotkeyPoller<string>.VkControl, F12));
            Assert.Empty(Step(poller, HotkeyPoller<string>.VkShift, HotkeyPoller<string>.VkAlt, F12));
        }

        [Fact]
        public void Releasing_shift_while_f12_is_held_counts_as_pressing_f12()
        {
            var poller = Poller();

            Assert.Equal(new[] { "pause" }, Step(poller, HotkeyPoller<string>.VkShift, F12));
            Assert.Equal(new[] { "record" }, Step(poller, F12));
        }

        [Fact]
        public void Gate_drops_the_second_report_of_one_press()
        {
            var gate = new HotkeyGate<string>(400);

            Assert.True(gate.Allow("record", 1000));
            Assert.False(gate.Allow("record", 1100));
            Assert.True(gate.Allow("pause", 1100));
            Assert.True(gate.Allow("record", 1500));
        }
    }
}

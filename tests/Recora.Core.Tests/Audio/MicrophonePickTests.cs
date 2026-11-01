using Recora.Core.Audio;

namespace Recora.Core.Tests.Audio
{
    public class MicrophonePickTests
    {
        private static readonly MicrophoneCandidate Headset = new MicrophoneCandidate("bt", HandsFree: true);
        private static readonly MicrophoneCandidate Webcam = new MicrophoneCandidate("cam", HandsFree: false);

        [Fact]
        public void Pick_DefaultIsTheBluetoothHeadset_UsesAnotherMicrophone()
        {
            Assert.Equal(new MicrophoneChoice("cam", false), MicrophonePick.Pick(new[] { Headset, Webcam }, string.Empty, "bt"));
        }

        [Fact]
        public void Pick_OnlyTheBluetoothHeadset_UsesItAndWarns()
        {
            Assert.Equal(new MicrophoneChoice("bt", true), MicrophonePick.Pick(new[] { Headset }, string.Empty, "bt"));
        }

        [Fact]
        public void Pick_UserChoseTheHeadset_KeepsItAndWarns()
        {
            Assert.Equal(new MicrophoneChoice("bt", true), MicrophonePick.Pick(new[] { Headset, Webcam }, "bt", "cam"));
        }

        [Fact]
        public void Pick_ChosenMicrophoneUnplugged_FallsBackToTheDefault()
        {
            Assert.Equal(new MicrophoneChoice("cam", false), MicrophonePick.Pick(new[] { Webcam }, "usb-gone", "cam"));
        }

        [Fact]
        public void Pick_NoMicrophone_Null()
        {
            Assert.Null(MicrophonePick.Pick(new MicrophoneCandidate[0], string.Empty, null));
        }
    }
}

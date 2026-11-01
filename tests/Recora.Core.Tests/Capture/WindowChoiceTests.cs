using Recora.Core.Capture;
using Xunit;

namespace Recora.Core.Tests.Capture
{
    public class WindowChoiceTests
    {
        private static readonly OpenWindow[] Windows =
        {
            new OpenWindow(1, "YouTube - Chrome", "chrome"),
            new OpenWindow(2, "Gmail - Chrome", "chrome"),
            new OpenWindow(3, "League of Legends (TM) Client", "League of Legends"),
        };

        [Fact]
        public void Same_program_and_title_wins()
        {
            Assert.Equal(2, WindowChoice.Find(Windows, "chrome", "Gmail - Chrome")!.Hwnd);
        }

        [Fact]
        public void Same_program_is_found_when_the_title_changed()
        {
            Assert.Equal(3, WindowChoice.Find(Windows, "league of legends", "League of Legends")!.Hwnd);
        }

        [Fact]
        public void Nothing_saved_or_program_closed_finds_nothing()
        {
            Assert.Null(WindowChoice.Find(Windows, string.Empty, string.Empty));
            Assert.Null(WindowChoice.Find(Windows, "obs64", "OBS"));
        }

        [Fact]
        public void Label_names_the_program_and_cuts_long_titles()
        {
            var window = new OpenWindow(9, new string('a', 80), "game");
            Assert.EndsWith("… · game.exe", window.Label);
            Assert.True(window.Label.Length < 75);
        }
    }
}

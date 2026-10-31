using System;
using System.Globalization;
using System.Windows.Forms;
using Recora.App.Logging;
using Recora.App.Native;
using Recora.App.UI;

namespace Recora.App
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var selfTest = OptionValue(args, "--selftest");
            var screenshot = OptionValue(args, "--ui-screenshot");

            if (selfTest != null || screenshot != null)
            {
                // A GUI-subsystem exe has no console; borrow the caller's so CI logs show progress.
                NativeMethods.AttachConsole(NativeMethods.AttachParentProcess);
            }

            if (selfTest != null)
            {
                return SelfTest.Run(selfTest);
            }

            if (screenshot != null)
            {
                var scale = float.Parse(OptionValue(args, "--ui-scale") ?? "1", CultureInfo.InvariantCulture);
                var page = (PageKind)Enum.Parse(typeof(PageKind), OptionValue(args, "--page") ?? nameof(PageKind.Home), ignoreCase: true);
                return UiScreenshot.Run(screenshot, scale, page);
            }

            Log.Initialize(Log.DefaultDirectory, echoToConsole: false);
            Application.Run(new MainForm());
            return 0;
        }

        private static string? OptionValue(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}

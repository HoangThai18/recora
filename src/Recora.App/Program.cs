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
            var crash = OptionValue(args, "--selftest-crash");
            var recover = OptionValue(args, "--recover");
            var roundtrip = OptionValue(args, "--settings-roundtrip");
            var screenshot = OptionValue(args, "--ui-screenshot");

            if (selfTest != null || crash != null || recover != null || roundtrip != null || screenshot != null)
            {
                // A GUI-subsystem exe has no console; borrow the caller's so CI logs show progress.
                NativeMethods.AttachConsole(NativeMethods.AttachParentProcess);
            }

            if (selfTest != null)
            {
                return SelfTest.Run(selfTest);
            }

            if (crash != null)
            {
                return SelfTest.Crash(crash);
            }

            if (recover != null)
            {
                return SelfTest.Recover(recover);
            }

            if (roundtrip != null)
            {
                return SelfTest.SettingsRoundtrip(roundtrip);
            }

            if (screenshot != null)
            {
                var scale = float.Parse(OptionValue(args, "--ui-scale") ?? "1", CultureInfo.InvariantCulture);
                var page = (PageKind)Enum.Parse(typeof(PageKind), OptionValue(args, "--page") ?? nameof(PageKind.Home), ignoreCase: true);
                return UiScreenshot.Run(screenshot, scale, page);
            }

            Log.Initialize(Log.DefaultDirectory, echoToConsole: false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => Report(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("unhandled exception", e.ExceptionObject as Exception);
            Application.Run(new MainForm());
            return 0;
        }

        /// <summary>Details go to the log; the user only sees a Vietnamese sentence, never a stack trace.</summary>
        private static void Report(Exception error)
        {
            Log.Error("unexpected error on the UI thread", error);
            MessageBox.Show(
                "Recora gặp lỗi không mong muốn nhưng vẫn chạy tiếp. Chi tiết đã ghi vào nhật ký.",
                "Recora",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static string? OptionValue(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}

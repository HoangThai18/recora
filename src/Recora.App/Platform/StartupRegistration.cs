using System.Windows.Forms;
using Microsoft.Win32;

namespace Recora.App.Platform
{
    /// <summary>"Mở cùng Windows": the per-user Run key, so no admin rights are needed.</summary>
    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Recora";

        public static void Apply(bool enabled)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled)
                {
                    key.SetValue(ValueName, $"\"{Application.ExecutablePath}\" --minimized");
                }
                else if (key.GetValue(ValueName) != null)
                {
                    key.DeleteValue(ValueName);
                }
            }
        }
    }
}

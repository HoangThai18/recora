using System.Drawing;
using System.IO;
using System.Reflection;

namespace Recora.App.UI
{
    /// <summary>The app icon, embedded in the exe (also set as the Win32 icon resource for Explorer and the taskbar).</summary>
    internal static class AppIcon
    {
        private static Icon? _large;
        private static Bitmap? _bitmap;

        public static Icon Large => _large ??= new Icon(Open("recora.ico"));

        public static Bitmap Bitmap => _bitmap ??= new Bitmap(Open("recora-256.png"));

        private static Stream Open(string name) =>
            Assembly.GetExecutingAssembly().GetManifestResourceStream("Recora.App.Assets." + name)
            ?? throw new FileNotFoundException("embedded resource missing", name);
    }
}

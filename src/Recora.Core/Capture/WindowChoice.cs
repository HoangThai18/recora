using System;
using System.Collections.Generic;
using System.Linq;

namespace Recora.Core.Capture
{
    /// <summary>An open top-level window as the Home list shows it.</summary>
    public sealed record OpenWindow(long Hwnd, string Title, string Process)
    {
        /// <summary>"League of Legends (TM) Client · League of Legends.exe", cut to fit the list.</summary>
        public string Label
        {
            get
            {
                var title = Title.Length > 60 ? Title.Substring(0, 57) + "…" : Title;
                return Process.Length == 0 ? title : title + " · " + Process + ".exe";
            }
        }
    }

    /// <summary>
    /// The saved window is kept as its program and title, since a window handle dies with the game: next session
    /// the same program's window is found again even when its title changed (a browser tab, a game lobby).
    /// </summary>
    public static class WindowChoice
    {
        public static OpenWindow? Find(IReadOnlyList<OpenWindow> windows, string process, string title)
        {
            if (string.IsNullOrEmpty(process))
            {
                return null;
            }

            var same = windows.Where(w => string.Equals(w.Process, process, StringComparison.OrdinalIgnoreCase)).ToList();
            return same.FirstOrDefault(w => w.Title == title) ?? same.FirstOrDefault();
        }
    }
}

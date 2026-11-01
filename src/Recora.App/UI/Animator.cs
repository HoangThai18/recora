using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Recora.Core.Animation;

namespace Recora.App.UI
{
    /// <summary>
    /// One UI timer for every moving thing in the window. It ticks only while something is moving and stops by itself,
    /// so an idle Recora (or one hidden in the tray while a game runs) costs nothing. Windows' "Show animations"
    /// setting is honoured: with it off, every change lands at once.
    /// </summary>
    internal static class Animator
    {
        private const uint SpiGetClientAreaAnimation = 0x1042;
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly Timer Timer = new Timer { Interval = 15 };
        private static readonly List<Entry> Running = new List<Entry>();
        private static bool? _systemAllows;

        static Animator()
        {
            Timer.Tick += (_, _) => Tick();
        }

        /// <summary>Screenshots and tests switch motion off so every picture shows the final state.</summary>
        public static bool Off { get; set; }

        public static TimeSpan Now => Clock.Elapsed;

        public static bool Enabled => !Off && (_systemAllows ??= SystemAllows());

        /// <summary>Moves <paramref name="tween"/> to <paramref name="target"/>, repainting <paramref name="owner"/> each frame.</summary>
        public static void Animate(Control owner, Tween tween, double target, int milliseconds)
        {
            if (!Enabled || !Showing(owner))
            {
                tween.Jump(target);
                owner.Invalidate();
                return;
            }

            tween.To(target, TimeSpan.FromMilliseconds(milliseconds), Now);
            Track(owner, tween, now => tween.Update(now));
        }

        /// <summary>
        /// A continuous motion (breathing, blinking) that repaints <paramref name="owner"/> while <paramref name="keepGoing"/>
        /// says so. It also stops whenever the window is hidden or minimized; the owner restarts it from its next paint.
        /// </summary>
        public static void Loop(Control owner, object key, Func<bool> keepGoing)
        {
            if (Enabled && Showing(owner))
            {
                Track(owner, key, _ => keepGoing());
            }
        }

        public static bool IsLooping(object key) => Running.Exists(e => ReferenceEquals(e.Key, key));

        private static void Track(Control owner, object key, Func<TimeSpan, bool> step)
        {
            if (!Running.Exists(e => ReferenceEquals(e.Key, key)))
            {
                Running.Add(new Entry(owner, key, step));
            }

            if (!Timer.Enabled)
            {
                Timer.Start();
            }
        }

        private static void Tick()
        {
            var now = Now;
            foreach (var entry in Running.ToArray())
            {
                var alive = !entry.Owner.IsDisposed && Showing(entry.Owner) && entry.Step(now);
                if (!entry.Owner.IsDisposed)
                {
                    entry.Owner.Invalidate();
                }

                if (!alive)
                {
                    Running.Remove(entry);
                }
            }

            if (Running.Count == 0)
            {
                Timer.Stop();
            }
        }

        /// <summary>On screen: created, visible all the way up, and its window not minimized.</summary>
        private static bool Showing(Control owner)
        {
            if (owner.IsDisposed || !owner.IsHandleCreated || !owner.Visible)
            {
                return false;
            }

            var form = owner.FindForm();
            return form != null && form.Visible && form.WindowState != FormWindowState.Minimized;
        }

        private static bool SystemAllows()
        {
            try
            {
                return !SystemParametersInfo(SpiGetClientAreaAnimation, 0, out var enabled, 0) || enabled;
            }
            catch (EntryPointNotFoundException)
            {
                return true;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);

        private sealed record Entry(Control Owner, object Key, Func<TimeSpan, bool> Step);
    }
}

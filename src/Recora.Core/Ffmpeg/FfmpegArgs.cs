using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Recora.Core.Ffmpeg
{
    /// <summary>
    /// An ordered FFmpeg argument list. .NET Framework only takes one command-line string, and ffmpeg.exe splits it
    /// with CommandLineToArgvW, so <see cref="ToCommandLine"/> quotes each argument by those rules.
    /// </summary>
    public sealed class FfmpegArgs
    {
        private readonly List<string> _items = new List<string>();

        public IReadOnlyList<string> Items => _items;

        public FfmpegArgs Add(params string[] args)
        {
            foreach (var arg in args)
            {
                _items.Add(arg ?? throw new ArgumentNullException(nameof(args)));
            }

            return this;
        }

        public FfmpegArgs Add(IEnumerable<string> args) => Add(args.ToArray());

        public FfmpegArgs AddIf(bool condition, params string[] args) => condition ? Add(args) : this;

        /// <summary>The value that follows <paramref name="option"/>, or null when the option is absent.</summary>
        public string? ValueOf(string option)
        {
            var index = _items.IndexOf(option);
            return index >= 0 && index + 1 < _items.Count ? _items[index + 1] : null;
        }

        public string ToCommandLine() => string.Join(" ", _items.Select(Quote));

        public override string ToString() => ToCommandLine();

        public static string Quote(string arg)
        {
            if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
            {
                return arg;
            }

            var quoted = new StringBuilder(arg.Length + 2).Append('"');
            var backslashes = 0;

            foreach (var c in arg)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }

                // Backslashes are literal unless they precede a quote, where each one must be doubled.
                quoted.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes).Append(c);
                backslashes = 0;
            }

            // Closing quote follows: trailing backslashes would escape it unless doubled.
            return quoted.Append('\\', backslashes * 2).Append('"').ToString();
        }
    }
}

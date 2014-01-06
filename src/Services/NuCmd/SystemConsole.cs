using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuCmd
{
    public class SystemConsole : IConsole
    {
        private TextWriter _error = new ConsoleWriter("error", ConsoleColor.Red, Console.Error);
        private TextWriter _warning = new ConsoleWriter("warn", ConsoleColor.Yellow, Console.Error);
        private TextWriter _info = new ConsoleWriter("info", ConsoleColor.Green, Console.Out);
        private TextWriter _help = new ConsoleWriter("help", ConsoleColor.Blue, Console.Error);
        private TextWriter _trace = new ConsoleWriter("trace", ConsoleColor.Gray, Console.Error);

        public TextWriter Error { get { return _error; } }
        public TextWriter Trace { get { return _trace; } }
        public TextWriter Warning { get { return _warning; } }
        public TextWriter Info { get { return _info; } }
        public TextWriter Help { get { return _help; } }

        internal class ConsoleWriter : TextWriter
        {
            private static ConcurrentBag<string> _prefixes = new ConcurrentBag<string>();
            private static Lazy<int> _maxPrefix = new Lazy<int>(() => _prefixes.Max(p => p.Length));

            private string _prefix;
            private TextWriter _console;
            private ConsoleColor _prefixColor;
            private Action<ConsoleColor> _setForegroundColor;
            private Func<ConsoleColor> _getForegroundColor;

            private char? _previous = null;

            public override Encoding Encoding
            {
                get { return _console.Encoding; }
            }

            public ConsoleWriter(string prefix, ConsoleColor prefixColor, TextWriter console)
                : this(prefix, prefixColor, console, c => Console.ForegroundColor = c, () => Console.ForegroundColor) { }

            public ConsoleWriter(string prefix, ConsoleColor prefixColor, TextWriter console, Action<ConsoleColor> setForegroundColor, Func<ConsoleColor> getForegroundColor)
            {
                _prefix = prefix;
                _console = console;
                _prefixColor = prefixColor;
                _setForegroundColor = setForegroundColor;
                _getForegroundColor = getForegroundColor;

                _prefixes.Add(prefix);
            }

            public override async Task WriteAsync(char value)
            {
                await WritePrefixIfNecessary(value);
                _previous = value;
                await _console.WriteAsync(value);
            }

            public override void Write(char value)
            {
                WriteAsync(value).Wait();
            }

            public override async Task WriteAsync(string value)
            {
                if (value != null)
                {
                    await WriteAsync(value.ToCharArray());
                }
            }

            public override async Task WriteAsync(char[] buffer, int index, int count)
            {
                if (buffer != null)
                {
                    for (int i = index; i < count; i++)
                    {
                        await WriteAsync(buffer[i]);
                    }
                }
            }

            public override async Task WriteLineAsync()
            {
                await WriteAsync(Environment.NewLine);
            }

            public override async Task WriteLineAsync(char value)
            {
                await WriteAsync(value);
                await WriteLineAsync();
            }

            public override async Task WriteLineAsync(char[] buffer, int index, int count)
            {
                if (buffer != null)
                {
                    for (int i = index; i < count; i++)
                    {
                        await WriteAsync(buffer[i]);
                    }
                    await WriteLineAsync();
                }
            }

            public override async Task WriteLineAsync(string value)
            {
                await WriteAsync(value);
                await WriteLineAsync();
            }

            private async Task WritePrefixIfNecessary(char value)
            {
                // Don't write prefix for \n in two-char newline
                if(_previous == null || (_previous == '\r' && value != '\n') || _previous == '\n')
                {
                    await WritePrefix();
                }
            }

            private async Task WritePrefix()
            {
                var old = _getForegroundColor();
                _setForegroundColor(_prefixColor);
                await _console.WriteAsync(_prefix.PadRight(_maxPrefix.Value));
                _setForegroundColor(old);

                await _console.WriteAsync(": ");
            }
        }
    }
}

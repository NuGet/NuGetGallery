using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NLog.Targets;
using ColorPair = System.Tuple<System.ConsoleColor, System.ConsoleColor?>;

namespace NuBot.Infrastructure
{
    public class SnazzyConsoleTarget : TargetWithLayout
    {
        private static readonly Dictionary<LogLevel, ColorPair> ColorTable = new Dictionary<LogLevel, ColorPair>()
        {
            { LogLevel.Debug, new ColorPair(ConsoleColor.Magenta, null) },
            { LogLevel.Error, new ColorPair(ConsoleColor.Red, null) },
            { LogLevel.Fatal, new ColorPair(ConsoleColor.White, ConsoleColor.Red) },
            { LogLevel.Info, new ColorPair(ConsoleColor.Green, null) },
            { LogLevel.Trace, new ColorPair(ConsoleColor.DarkGray, null) },
            { LogLevel.Warn, new ColorPair(ConsoleColor.Black, ConsoleColor.Yellow) }
        };

        private static readonly Dictionary<LogLevel, string> LevelNames = new Dictionary<LogLevel, string>() {
            { LogLevel.Debug, "debug" },
            { LogLevel.Error, "error" },
            { LogLevel.Fatal, "fatal" },
            { LogLevel.Info, "info" },
            { LogLevel.Trace, "trace" },
            { LogLevel.Warn, "warn" },
        };

        private static readonly int LevelLength = LevelNames.Values.Max(s => s.Length);

        protected override void Write(LogEventInfo logEvent)
        {
            var oldForeground = Console.ForegroundColor;
            var oldBackground = Console.BackgroundColor;

            // Get us to the start of a line
            if (Console.CursorLeft > 0)
            {
                Console.WriteLine();
            }

            // Get Color Pair colors
            ColorPair pair;
            if (!ColorTable.TryGetValue(logEvent.Level, out pair))
            {
                pair = new ColorPair(Console.ForegroundColor, Console.BackgroundColor);
            }
            if (pair.Item2 == null)
            {
                pair = new ColorPair(pair.Item1, Console.BackgroundColor);
            }

            // Get level string
            string levelName;
            if (!LevelNames.TryGetValue(logEvent.Level, out levelName))
            {
                levelName = logEvent.Level.ToString();
            }
            levelName = levelName.PadRight(LevelLength).Substring(0, LevelLength);

            // Break the message in to lines as necessary
            var message = Layout.Render(logEvent);
            var lines = new List<string>();
            var prefix = levelName + ": ";
            var fullMessage = prefix + message;
            var maxWidth = Console.BufferWidth - 2;
            while (fullMessage.Length > maxWidth)
            {
                int end = maxWidth - prefix.Length;
                int spaceIndex = message.LastIndexOf(' ', Math.Min(end, message.Length - 1));
                if (spaceIndex < 10)
                {
                    spaceIndex = end;
                }
                lines.Add(message.Substring(0, spaceIndex).Trim());
                message = message.Substring(spaceIndex).Trim();
                fullMessage = prefix + message;
            }
            lines.Add(message);

            // Write lines
            bool first = true;
            foreach (var line in lines.Where(l => !String.IsNullOrWhiteSpace(l)))
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    Console.WriteLine();
                }

                // Write Level
                Console.ForegroundColor = pair.Item1;
                Console.BackgroundColor = pair.Item2.Value;
                Console.Write(levelName);

                // Write the message using the default foreground color, but the specified background color
                // UNLESS: The background color has been changed. In which case the foreground color applies here too
                var foreground = pair.Item2.Value == Console.BackgroundColor
                                        ? Console.ForegroundColor
                                        : pair.Item1;
                Console.ForegroundColor = foreground;
                Console.Write(": " + line);
            }
            Console.WriteLine();
            
            Console.ForegroundColor = oldForeground;
            Console.BackgroundColor = oldBackground;
        }
    }
}

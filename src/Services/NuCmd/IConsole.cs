using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace NuCmd
{
    public interface IConsole
    {
        TextWriter Error { get; }
        TextWriter Fatal { get; }
        TextWriter Trace { get; }
        TextWriter Warning { get; }
        TextWriter Info { get; }
        TextWriter Help { get; }
        TextWriter Http { get; }

        Task WriteObject(object obj, IConsoleFormatter formatter);
        Task WriteObjects(IEnumerable<object> objs, IConsoleFormatter formatter);
        Task WriteTable(ConsoleTable table);
        Task WriteTable<T>(IEnumerable<T> objs, Func<T, object> selector);
    }

    public static class ConsoleExtensions
    {
        public static Task WriteObject(this IConsole self, object obj)
        {
            return self.WriteObject(obj, DefaultConsoleFormatter.Instance);
        }

        public static Task WriteObjects(this IConsole self, IEnumerable<object> obj)
        {
            return self.WriteObjects(obj, DefaultConsoleFormatter.Instance);
        }

        public static Task WriteError(this IConsole self, string format, params object[] args)
        {
            return WriteError(self, String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteError(this IConsole self, string message)
        {
            return self.Error.WriteAsync(message);
        }

        public static Task WriteFatal(this IConsole self, string format, params object[] args)
        {
            return WriteFatal(self, String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteFatal(this IConsole self, string message)
        {
            return self.Fatal.WriteAsync(message);
        }

        public static Task WriteTrace(this IConsole self, string format, params object[] args)
        {
            return WriteTrace(self, String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteTrace(this IConsole self, string message)
        {
            return self.Trace.WriteAsync(message);
        }

        public static Task WriteWarning(this IConsole self, string format, params object[] args)
        {
            return WriteWarning(self, String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteWarning(this IConsole self, string message)
        {
            return self.Warning.WriteAsync(message);
        }

        public static Task WriteInfo(this IConsole self, string format, params object[] args)
        {
            return WriteInfo(self, String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteInfo(this IConsole self, string message)
        {
            return self.Info.WriteAsync(message);
        }

        public static Task WriteHelp(this IConsole self, string format, params object[] args)
        {
            return WriteHelp(self, String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteHelp(this IConsole self, string message)
        {
            return self.Help.WriteAsync(message);
        }

        public static Task WriteHttp(this IConsole self, string format, params object[] args)
        {
            return WriteHttp(self, String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteHttp(this IConsole self, string message)
        {
            return self.Http.WriteAsync(message);
        }

        public static Task WriteErrorLine(this IConsole self) { return WriteErrorLine(self, String.Empty); }
        public static Task WriteErrorLine(this IConsole self, string message) { return self.Error.WriteLineAsync(message); }
        public static Task WriteErrorLine(this IConsole self, string format, params object[] args)
        {
            return self.Error.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteFatalLine(this IConsole self) { return WriteFatalLine(self, String.Empty); }
        public static Task WriteFatalLine(this IConsole self, string message) { return self.Fatal.WriteLineAsync(message); }
        public static Task WriteFatalLine(this IConsole self, string format, params object[] args)
        {
            return self.Fatal.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteTraceLine(this IConsole self) { return WriteTraceLine(self, String.Empty); }
        public static Task WriteTraceLine(this IConsole self, string message) { return self.Trace.WriteLineAsync(message); }
        public static Task WriteTraceLine(this IConsole self, string format, params object[] args)
        {
            return self.Trace.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteWarningLine(this IConsole self) { return WriteHelpLine(self, String.Empty); }
        public static Task WriteWarningLine(this IConsole self, string message) { return self.Warning.WriteLineAsync(message); }
        public static Task WriteWarningLine(this IConsole self, string format, params object[] args)
        {
            return self.Warning.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteInfoLine(this IConsole self) { return WriteInfoLine(self, String.Empty); }
        public static Task WriteInfoLine(this IConsole self, string message) { return self.Info.WriteLineAsync(message); }
        public static Task WriteInfoLine(this IConsole self, string format, params object[] args)
        {
            return self.Info.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteHelpLine(this IConsole self) { return WriteHelpLine(self, String.Empty); }
        public static Task WriteHelpLine(this IConsole self, string message) { return self.Help.WriteLineAsync(message); }
        public static Task WriteHelpLine(this IConsole self, string format, params object[] args)
        {
            return self.Help.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteHttpLine(this IConsole self) { return WriteHttpLine(self, String.Empty); }
        public static Task WriteHttpLine(this IConsole self, string message) { return self.Http.WriteLineAsync(message); }
        public static Task WriteHttpLine(this IConsole self, string format, params object[] args)
        {
            return self.Http.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }
    }
}

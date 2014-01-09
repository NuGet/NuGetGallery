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
        TextWriter Trace { get; }
        TextWriter Warning { get; }
        TextWriter Info { get; }
        TextWriter Help { get; }
        TextWriter Http { get; }

        Task WriteObject(object obj, IConsoleFormatter formatter);
        Task WriteObjects(IEnumerable<object> objs, IConsoleFormatter formatter);
        Task WriteTable(ConsoleTable table);
        Task WriteTable<T>(IEnumerable<T> objs, params Expression<Func<T, object>>[] columns);
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
            return self.Error.WriteAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteTrace(this IConsole self, string format, params object[] args)
        {
            return self.Trace.WriteAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteWarning(this IConsole self, string format, params object[] args)
        {
            return self.Warning.WriteAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteInfo(this IConsole self, string format, params object[] args)
        {
            return self.Info.WriteAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteHelp(this IConsole self, string format, params object[] args)
        {
            return self.Help.WriteAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteErrorLine(this IConsole self) { return WriteErrorLine(self, String.Empty); }
        public static Task WriteErrorLine(this IConsole self, string format, params object[] args)
        {
            return self.Error.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteTraceLine(this IConsole self) { return WriteTraceLine(self, String.Empty); }
        public static Task WriteTraceLine(this IConsole self, string format, params object[] args)
        {
            return self.Trace.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteWarningLine(this IConsole self) { return WriteHelpLine(self, String.Empty); }
        public static Task WriteWarningLine(this IConsole self, string format, params object[] args)
        {
            return self.Warning.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteInfoLine(this IConsole self) { return WriteInfoLine(self, String.Empty); }
        public static Task WriteInfoLine(this IConsole self, string format, params object[] args)
        {
            return self.Info.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteHelpLine(this IConsole self) { return WriteHelpLine(self, String.Empty); }
        public static Task WriteHelpLine(this IConsole self, string format, params object[] args)
        {
            return self.Help.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }

        public static Task WriteHttpLine(this IConsole self) { return WriteHttpLine(self, String.Empty); }
        public static Task WriteHttpLine(this IConsole self, string format, params object[] args)
        {
            return self.Http.WriteLineAsync(String.Format(CultureInfo.CurrentCulture, format, args));
        }
    }
}

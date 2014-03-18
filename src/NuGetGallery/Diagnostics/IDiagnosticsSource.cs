using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using NuGet;

namespace NuGetGallery.Diagnostics
{
    public interface IDiagnosticsSource
    {
        void TraceEvent(TraceEventType type, int id, string message, [CallerMemberName] string member = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0);

        void PerfEvent(string name, TimeSpan time, IEnumerable<KeyValuePair<string, object>> payload);
    }

    public static class DiagnosticsSourceExtensions
    {
        private static int _activityId = 0;

        // That's right. Regions. Deal with it.
        #region TraceEventType.Critical
        public static void Critical(this IDiagnosticsSource self,
                                    string message,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(TraceEventType.Critical, id: 0, message: message, member: member, file: file, line: line);
        }

        public static void Critical(this IDiagnosticsSource self,
                                    string message,
                                    int id,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(TraceEventType.Critical, id, message, member, file, line);
        }

        public static void Critical(this IDiagnosticsSource self,
                                    Exception ex,
                                    string context,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            Critical(
                self,
                message: String.Format(CultureInfo.CurrentCulture, "{0}: {1}", context, ex),
                member: member,
                file: file,
                line: line);
        }
        #endregion
        #region TraceEventType.Error
        public static void Error(this IDiagnosticsSource self,
                                    string message,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(TraceEventType.Error, id: 0, message: message, member: member, file: file, line: line);
        }

        public static void Error(this IDiagnosticsSource self,
                                    string message,
                                    int id,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(TraceEventType.Error, id, message, member, file, line);
        }

        public static void Error(this IDiagnosticsSource self,
                                    Exception ex,
                                    string context,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            Error(
                self,
                message: String.Format(CultureInfo.CurrentCulture, "{0}: {1}", context, ex),
                member: member,
                file: file,
                line: line);
        }
        #endregion
        #region TraceEventType.Warning
        public static void Warning(this IDiagnosticsSource self,
                                    string message,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(TraceEventType.Warning, id: 0, message: message, member: member, file: file, line: line);
        }

        public static void Warning(this IDiagnosticsSource self,
                                    string message,
                                    int id,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(TraceEventType.Warning, id, message, member, file, line);
        }

        public static void Warning(this IDiagnosticsSource self,
                                    Exception ex,
                                    string context,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            Warning(
                self,
                message: String.Format(CultureInfo.CurrentCulture, "{0}: {1}", context, ex),
                member: member,
                file: file,
                line: line);
        }
        #endregion
        #region TraceEventType.Information
        public static void Information(this IDiagnosticsSource self,
                                    string message,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(TraceEventType.Information, id: 0, message: message, member: member, file: file, line: line);
        }

        public static void Information(this IDiagnosticsSource self,
                                    string message,
                                    int id,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(TraceEventType.Information, id, message, member, file, line);
        }

        public static void Information(this IDiagnosticsSource self,
                                    Exception ex,
                                    string context,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            Information(
                self,
                message: String.Format(CultureInfo.CurrentCulture, "{0}: {1}", context, ex),
                member: member,
                file: file,
                line: line);
        }
        #endregion
        #region TraceEventType.Verbose
        public static void Verbose(this IDiagnosticsSource self,
                                    string message,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(TraceEventType.Verbose, id: 0, message: message, member: member, file: file, line: line);
        }

        public static void Verbose(this IDiagnosticsSource self,
                                    string message,
                                    int id,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(TraceEventType.Verbose, id, message, member, file, line);
        }

        public static void Verbose(this IDiagnosticsSource self,
                                    Exception ex,
                                    string context,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            Verbose(
                self,
                message: String.Format(CultureInfo.CurrentCulture, "{0}: {1}", context, ex),
                member: member,
                file: file,
                line: line);
        }
        #endregion

        public static IDisposable Activity(this IDiagnosticsSource self,
                                           string name,
                                           [CallerMemberName] string member = null,
                                           [CallerFilePath] string file = null,
                                           [CallerLineNumber] int line = 0)
        {
            var thisActivityId = Interlocked.Increment(ref _activityId);
            var start = DateTime.UtcNow;
            self.TraceEvent(TraceEventType.Start,
                       id: thisActivityId,
                       message: String.Format(CultureInfo.CurrentCulture, "Starting {0}", name),
                       member: member,
                       file: file,
                       line: line);
            return new DisposableAction(() =>
            {
                var diff = DateTime.UtcNow - start;
                var stopMessage = String.Format(CultureInfo.CurrentCulture, "Finished {0}. Duration {1:0.00}ms", name, diff.TotalMilliseconds);
                self.TraceEvent(TraceEventType.Stop,
                            id: thisActivityId,
                            message: stopMessage,
                            member: member,
                            file: file,
                            line: line);
            });
        }
    }
}
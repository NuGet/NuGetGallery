// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace NuGetGallery.Diagnostics
{
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
            self.TraceEvent(LogLevel.Critical, eventId: 0, message: message, member: member, file: file, line: line);
        }

        public static void Critical(this IDiagnosticsSource self,
                                    string message,
                                    EventId eventId,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(LogLevel.Critical, eventId, message, member, file, line);
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
                message: string.Format(CultureInfo.CurrentCulture, "{0}: {1}", context, ex),
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
            self.TraceEvent(LogLevel.Error, eventId: 0, message: message, member: member, file: file, line: line);
        }

        public static void Error(this IDiagnosticsSource self,
                                    string message,
                                    EventId eventId,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(LogLevel.Error, eventId, message, member, file, line);
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
                message: string.Format(CultureInfo.CurrentCulture, "{0}: {1}", context, ex),
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
            self.TraceEvent(LogLevel.Warning, eventId: 0, message: message, member: member, file: file, line: line);
        }

        public static void Warning(this IDiagnosticsSource self,
                                    string message,
                                    EventId eventId,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(LogLevel.Warning, eventId, message, member, file, line);
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
                message: string.Format(CultureInfo.CurrentCulture, "{0}: {1}", context, ex),
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
            self.TraceEvent(LogLevel.Information, eventId: 0, message: message, member: member, file: file, line: line);
        }

        public static void Information(this IDiagnosticsSource self,
                                    string message,
                                    EventId eventId,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(LogLevel.Information, eventId, message, member, file, line);
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
                message: string.Format(CultureInfo.CurrentCulture, "{0}: {1}", context, ex),
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
            self.TraceEvent(LogLevel.Trace, eventId: 0, message: message, member: member, file: file, line: line);
        }

        public static void Verbose(this IDiagnosticsSource self,
                                    string message,
                                    EventId eventId,
                                    [CallerMemberName] string member = null,
                                    [CallerFilePath] string file = null,
                                    [CallerLineNumber] int line = 0)
        {
            self.TraceEvent(LogLevel.Trace, eventId, message, member, file, line);
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
                message: string.Format(CultureInfo.CurrentCulture, "{0}: {1}", context, ex),
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
            self.TraceEvent(LogLevel.Trace,
                       eventId: thisActivityId,
                       message: string.Format(CultureInfo.CurrentCulture, "Starting {0}", name),
                       member: member,
                       file: file,
                       line: line);

            return new DisposableAction(() =>
            {
                var diff = DateTime.UtcNow - start;
                var stopMessage = string.Format(CultureInfo.CurrentCulture, "Finished {0}. Duration {1:0.00}ms", name, diff.TotalMilliseconds);
                self.TraceEvent(LogLevel.Trace,
                            eventId: thisActivityId,
                            message: stopMessage,
                            member: member,
                            file: file,
                            line: line);
            });
        }
    }
}
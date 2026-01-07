// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace NuGetGallery.Diagnostics
{
    /// <summary>
    /// Gallery diagnostics source. 
    /// Trace events (including LogError extension) and Exception events are tracked in ApplicationInsights.
    /// 
    /// ILogger implementation based on https://github.com/aspnet/Logging/tree/master/src/Microsoft.Extensions.Logging.TraceSource 
    /// </summary>
    public sealed class TraceDiagnosticsSource : IDiagnosticsSource
    {
        private readonly ITelemetryClient _telemetryClient;
        private readonly string _name;

        public TraceDiagnosticsSource(string name, ITelemetryClient telemetryClient)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        }

        /// <summary>
        /// Write exception to ApplicationInsights.
        /// </summary>
        public void ExceptionEvent(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            _telemetryClient.TrackException(exception);
        }

        /// <summary>
        /// Write a trace event to ApplicationInsights.
        /// </summary>
        public void TraceEvent(
            LogLevel logLevel,
            EventId eventId,
            string message,
            [CallerMemberName] string member = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, ServicesStrings.ParameterCannotBeNullOrEmpty, nameof(message)),
                    nameof(message));
            }

            _telemetryClient.TrackTrace(FormatMessage(message, member, file, line), logLevel, eventId);
        }

        private static string FormatMessage(string message, string member, string file, int line)
        {
            return string.Format(CultureInfo.CurrentCulture, "[{0}:{1} in {2}] {3}", file, line, member, message);
        }

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            TraceEvent(logLevel, eventId, message);
        }

        bool ILogger.IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        IDisposable ILogger.BeginScope<TState>(TState state)
        {
            throw new NotSupportedException();
        }
    }
}
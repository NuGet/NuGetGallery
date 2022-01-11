// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NuGet.Services.Logging;
using NuGetGallery.Diagnostics;

namespace NuGet.Jobs
{
    public class LoggerDiagnosticsSource : IDiagnosticsSource
    {
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger _logger;

        public LoggerDiagnosticsSource(ITelemetryClient telemetryClient, ILogger logger)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void ExceptionEvent(Exception exception)
        {
            _telemetryClient.TrackException(exception);
        }

        public void PerfEvent(
            string name,
            TimeSpan time,
            IEnumerable<KeyValuePair<string, object>> payload)
        {
            _telemetryClient.TrackMetric(
                name,
                time.TotalMilliseconds,
                GetProperties(payload));
        }

        public void TraceEvent(
            TraceEventType type,
            int id,
            string message,
            [CallerMemberName] string member = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            _logger.Log(
                TraceEventTypeToLogLevel(type),
                id,
                state: new TraceState(type, id, message, member, file, line),
                exception: null,
                formatter: (s, e) => s.Message);
        }

        public void TraceEvent(
            LogLevel logLevel,
            EventId eventId,
            string message,
            [CallerMemberName] string member = null,
            [CallerFilePath] string file = null,
            [CallerLineNumber] int line = 0)
        {
            _logger.Log(
                logLevel,
                eventId.Id,
                state: new TraceState(LogLevelToTraceEventType(logLevel), eventId.Id, message, member, file, line),
                exception: null,
                formatter: (s, e) => s.Message);
        }

        private static TraceEventType LogLevelToTraceEventType(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical:
                    return TraceEventType.Critical;
                case LogLevel.Error:
                    return TraceEventType.Error;
                case LogLevel.Warning:
                    return TraceEventType.Warning;
                case LogLevel.Debug:
                    return TraceEventType.Verbose;
                default:
                    return TraceEventType.Information;
            }
        }

        private static LogLevel TraceEventTypeToLogLevel(TraceEventType type)
        {
            switch (type)
            {
                case TraceEventType.Critical:
                    return LogLevel.Critical;
                case TraceEventType.Error:
                    return LogLevel.Error;
                case TraceEventType.Warning:
                    return LogLevel.Warning;
                case TraceEventType.Information:
                    return LogLevel.Information;
                default:
                    return LogLevel.Trace;
            }
        }

        private static IDictionary<string, string> GetProperties(
            IEnumerable<KeyValuePair<string, object>> payload)
        {
            if (payload == null)
            {
                return null;
            }

            var dictionary = new Dictionary<string, string>();
            foreach (var pair in payload)
            {
                dictionary[pair.Key] = pair.Value?.ToString();
            }

            return dictionary;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            _logger.Log(logLevel, eventId, state, exception, formatter);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _logger.IsEnabled(logLevel);
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return _logger.BeginScope(state);
        }

        /// <summary>
        /// The parameters provided to <see cref="TraceEvent(TraceEventType, int, string, string, string, int)"/>. This
        /// type implements <see cref="IEnumerable{KeyValuePair{string, object}}" /> because Serilog uses this to
        /// populate the Application Insights custom dimensions property bag.
        /// </summary>
        public class TraceState : IEnumerable<KeyValuePair<string, object>>
        {
            private readonly Dictionary<string, object> _pairs;

            public TraceState(TraceEventType traceEventType, int id, string message, string member, string file, int line)
            {
                TraceEventType = traceEventType;
                Id = id;
                Message = message;
                Member = member;
                File = file;
                Line = line;

                _pairs = new Dictionary<string, object>
                {
                    { nameof(TraceEventType), TraceEventType },
                    { nameof(Id), Id },
                    { nameof(Message), Message },
                    { nameof(Member), Member },
                    { nameof(File), File },
                    { nameof(Line), Line },
                };
            }

            public TraceEventType TraceEventType { get; }
            public int Id { get; }
            public string Message { get; }
            public string Member { get; }
            public string File { get; }
            public int Line { get; }

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                return _pairs.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _pairs.GetEnumerator();
            }
        }
    }
}

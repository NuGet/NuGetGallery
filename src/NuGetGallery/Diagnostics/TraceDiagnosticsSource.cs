// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NuGetGallery.Services.Telemetry;

namespace NuGetGallery.Diagnostics
{
    /// <summary>
    /// Gallery diagnostics source. Trace events (including LogError extension) use System.Diagnostics traces, whereas
    /// Exception events are tracked as exceptions in ApplicationInsights. Eventually this class should be updated to
    /// use ApplicationInsights for trace events for consistency across the Gallery.
    /// 
    /// ILogger implementation based on https://github.com/aspnet/Logging/tree/master/src/Microsoft.Extensions.Logging.TraceSource 
    /// </summary>
    public class TraceDiagnosticsSource : IDiagnosticsSource, IDisposable
    {
        private const string ObjectName = "TraceDiagnosticsSource";
        private TraceSource _source;
        private ITelemetryClient _telemetryClient;

        public string Name { get; private set; }

        public TraceDiagnosticsSource(string name, ITelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            Name = name;
            _source = new TraceSource(name, SourceLevels.All);

            // Make the source's listeners list look like the global list.
            _source.Listeners.Clear();
            _source.Listeners.AddRange(Trace.Listeners);
        }

        /// <summary>
        /// Write exception to ApplicationInsights.
        /// 
        /// Note that Source.Error (DiagnosticsSourceExtensions) currently uses TraceEvent instead.
        /// </summary>
        public virtual void ExceptionEvent(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            _telemetryClient.TrackException(exception);
        }

        /// <summary>
        /// Write a System.Diagnostics trace event.
        /// </summary>
        public virtual void TraceEvent(TraceEventType type, int id, string message, [CallerMemberName] string member = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            if (_source == null)
            {
                throw new ObjectDisposedException(ObjectName);
            }
            if (String.IsNullOrEmpty(message))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.ParameterCannotBeNullOrEmpty, nameof(message)), nameof(message));
            }

            _source.TraceEvent(type, id, FormatMessage(message, member, file, line));
        }

        public void PerfEvent(string name, TimeSpan time, IEnumerable<KeyValuePair<string,object>> payload)
        {
            // For now, hard-code the number of samples we track to 1000.
            PerfCounters.AddSample(name, sampleSize: 1000, value: time.TotalMilliseconds);

            // Send the event to the queue
            MessageQueue.Enqueue(Name, new PerfEvent(name, DateTime.UtcNow, time, payload));
        }

        // The "protected virtual Dispose(bool)" pattern is for objects with unmanaged resources. We have none, so a virtual Dispose is enough.
        public virtual void Dispose()
        {
            // If we don't do this, it's not the end of the world, but if consumers do dispose us, flush and close the source
            if (_source != null)
            {
                _source.Flush();
                _source.Close();
                _source = null;
            }

            // We don't have a finalizer, but subclasses might. Those subclasses should have their Finalizer call Dispose, so suppress finalization
            GC.SuppressFinalize(this);
        }

        private static string FormatMessage(string message, string member, string file, int line)
        {
            return String.Format(CultureInfo.CurrentCulture, "[{0}:{1} in {2}] {3}", file, line, member, message);
        }

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            TraceEvent(LogLevelToTraceEventType(logLevel), eventId.Id, message);
        }

        bool ILogger.IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        IDisposable ILogger.BeginScope<TState>(TState state)
        {
            return new TraceDiagnosticsSourceScope(state);
        }

        private static TraceEventType LogLevelToTraceEventType(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Critical: return TraceEventType.Critical;
                case LogLevel.Error: return TraceEventType.Error;
                case LogLevel.Warning: return TraceEventType.Warning;
                case LogLevel.Information: return TraceEventType.Information;
                case LogLevel.Trace:
                default: return TraceEventType.Verbose;
            }
        }
    }
}
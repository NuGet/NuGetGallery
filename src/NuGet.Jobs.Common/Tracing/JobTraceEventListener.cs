// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;

namespace NuGet.Jobs
{
    /// <summary>
    /// This event listener may be used by jobs to channel event logs into standard tracing
    /// </summary>
    internal sealed class JobTraceEventListener
        : EventListener
    {
        /// <summary>
        /// {0} would be eventId. {1} would be the formatted event message
        /// Formatted event would be '[&lt;eventId&gt;]: &lt;message&gt;'
        /// </summary>
        private const string _eventLogFormat = "[{0}]: {1}";
        private readonly JobTraceListener _jobTraceListener;

        public JobTraceEventListener(JobTraceListener jobTraceListener)
        {
            _jobTraceListener = jobTraceListener;
        }

        private string GetFormattedEventLog(EventWrittenEventArgs eventData)
        {
            return string.Format(CultureInfo.InvariantCulture, _eventLogFormat, eventData.EventId, string.Format(eventData.Message, eventData.Payload.ToArray()));
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            switch (eventData.Level)
            {
                case EventLevel.Critical:
                case EventLevel.Error:
                    Trace.TraceError(_jobTraceListener.GetFormattedMessage(GetFormattedEventLog(eventData), excludeTimestamp: true));
                    break;
                case EventLevel.Warning:
                    Trace.TraceWarning(_jobTraceListener.GetFormattedMessage(GetFormattedEventLog(eventData), excludeTimestamp: true));
                    break;
                case EventLevel.LogAlways:
                case EventLevel.Informational:
                    Trace.TraceInformation(_jobTraceListener.GetFormattedMessage(GetFormattedEventLog(eventData), excludeTimestamp: true));
                    break;
                case EventLevel.Verbose:
                    Trace.WriteLine(_jobTraceListener.GetFormattedMessage(GetFormattedEventLog(eventData), excludeTimestamp: true));
                    break;
            }
        }
    }
}
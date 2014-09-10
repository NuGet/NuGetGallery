using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;

namespace NuGet.Jobs.Common
{
    /// <summary>
    /// All the jobs MUST use this logger. Since logs from  all the jobs get written to the same file
    /// We want to ensure that the logs are prefixed with jobName, startTime and more as needed
    /// </summary>
    public class JobTraceLogger : TraceListener
    {
        protected readonly string LogPrefix;
        /// <summary>
        /// {0} would be the log prefix. Currently, the prefix is '/<jobName>-<startTime>/'
        /// {1} would be the actual log message
        /// Formatted message would be of the form '/<jobName>-<startTime>//<message>'
        /// </summary>
        protected const string LogFormat = "[{0}]: {1}";
        private const string MessageWithTraceEventTypeFormat = "[{0}]: {1}";
        public JobTraceLogger(string jobName)
        {
            this.TraceOutputOptions = TraceOptions.DateTime;
            Trace.Listeners.Add(this);
            LogPrefix = String.Format("/{0}-{1}/", jobName, DateTime.UtcNow.ToString("O"));
        }

        public string GetFormattedMessage(string message)
        {
            return String.Format(LogFormat, DateTime.UtcNow.ToString("O"), message);
        }

        public string GetFormattedMessage(string format, params object[] args)
        {
            return GetFormattedMessage(String.Format(format, args));
        }

        protected string MessageWithTraceEventType(TraceEventType traceEventType, string message)
        {
            return String.Format(MessageWithTraceEventTypeFormat, traceEventType.ToString(), message);
        }

        [Conditional("TRACE")]
        public virtual void Flush(bool skipCurrentBatch)
        {
            // Check AzureBlobJobTraceLogger
        }

        /// <summary>
        /// FlushAll should NEVER get called until after all the logging is done
        /// </summary>
        [Conditional("TRACE")]
        public virtual void FlushAll()
        {
            // Check AzureBlobJobTraceLogger
            Trace.Listeners.Clear();
        }

        public override void Write(string message)
        {
            // Do Nothing
        }

        public override void WriteLine(string message)
        {
            // Do Nothing
        }
    }

    /// <summary>
    /// This event listener may be used by jobs to channel event logs into standard tracing
    /// </summary>
    public class JobTraceEventListener : EventListener
    {
        private readonly JobTraceLogger Logger;
        /// <summary>
        /// {0} would be eventId. {1} would be the formatted event message
        /// Formatted event would be '[<eventId>]: <message>'
        /// </summary>
        private const string EventLogFormat = "[{0}]: {1}";
        public JobTraceEventListener(JobTraceLogger logger)
        {
            Logger = logger;
        }

        private string GetFormattedEventLog(EventWrittenEventArgs eventData)
        {
            return String.Format(EventLogFormat, eventData.EventId, String.Format(eventData.Message, eventData.Payload.ToArray()));
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            switch (eventData.Level)
            {
                case EventLevel.Critical:
                case EventLevel.Error:
                    Trace.TraceError(Logger.GetFormattedMessage(GetFormattedEventLog(eventData)));
                    break;
                case EventLevel.Warning:
                    Trace.TraceWarning(Logger.GetFormattedMessage(GetFormattedEventLog(eventData)));
                    break;
                case EventLevel.LogAlways:
                case EventLevel.Informational:
                    Trace.TraceInformation(Logger.GetFormattedMessage(GetFormattedEventLog(eventData)));
                    break;
                case EventLevel.Verbose:
                    Trace.WriteLine(Logger.GetFormattedMessage(GetFormattedEventLog(eventData)));
                    break;
                default:
                    // DO Nothing
                    break;
            }
        }
    }
}

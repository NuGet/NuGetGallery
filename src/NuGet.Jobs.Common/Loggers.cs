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
    public class JobTraceLogger
    {
        protected readonly string LogPrefix;
        /// <summary>
        /// {0} would be the log prefix. Currently, the prefix is '/<jobName>-<startTime>/'
        /// {1} would be the actual log message
        /// Formatted message would be of the form '/<jobName>-<startTime>//<message>'
        /// </summary>
        protected const string LogFormat = "{0}//{1}";
        public JobTraceLogger(string jobName)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
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

        [Conditional("TRACE")]
        public virtual void Log(TraceLevel traceLevel, string message)
        {
            switch (traceLevel)
            {
                case TraceLevel.Error:
                    Trace.TraceError(GetFormattedMessage(message));
                    break;
                case TraceLevel.Warning:
                    Trace.TraceWarning(GetFormattedMessage(message));
                    break;
                case TraceLevel.Info:
                    Trace.TraceInformation(GetFormattedMessage(message));
                    break;
                case TraceLevel.Verbose:
                    Trace.WriteLine(GetFormattedMessage(message));
                    break;
                case TraceLevel.Off:
                default:
                    // Trace nothing
                    break;
            }
        }

        [Conditional("TRACE")]
        public virtual void Log(TraceLevel traceLevel, string format, params object[] args)
        {
            var message = String.Format(format, args);
            Log(traceLevel, message);
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
                    Logger.Log(TraceLevel.Error, GetFormattedEventLog(eventData));
                    break;
                case EventLevel.Warning:
                    Logger.Log(TraceLevel.Warning, GetFormattedEventLog(eventData));
                    break;
                case EventLevel.LogAlways:
                case EventLevel.Informational:
                    Logger.Log(TraceLevel.Info, GetFormattedEventLog(eventData));
                    break;
                case EventLevel.Verbose:
                    Logger.Log(TraceLevel.Verbose, GetFormattedEventLog(eventData));
                    break;
                default:
                    // DO Nothing
                    break;
            }
        }
    }
}

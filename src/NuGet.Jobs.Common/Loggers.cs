using System;
using System.Collections.Generic;
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
        protected const int LogTraceEventTypeHeaderLength = 12;
        protected readonly string LogPrefix;
        /// <summary>
        /// {0} would be the log prefix. Currently, the prefix is '/<jobName>-<startTime>/'
        /// {1} would be the actual log message
        /// Formatted message would be of the form '/<jobName>-<startTime>//<message>'
        /// </summary>
        protected const string LogFormat = "[{0}]: {1}";

        private static readonly string MessageWithTraceEventTypeFormat = "{0, -" + LogTraceEventTypeHeaderLength + "}{1}";
        private static readonly Dictionary<TraceEventType, string> TraceEventTypeStrings = new Dictionary<TraceEventType, string>()
        {
          { TraceEventType.Critical, "[Err]:" },
          { TraceEventType.Error, "[Err]:" },
          { TraceEventType.Information, "[Info]:" },
          { TraceEventType.Verbose, "[Verbose]:" },
          { TraceEventType.Warning, "[Warn]:" },
        };

        public JobTraceLogger(string jobName)
        {
            this.TraceOutputOptions = TraceOptions.DateTime;
            Trace.Listeners.Add(this);
            LogPrefix = String.Format("/{0}-{1}/", jobName, DateTime.UtcNow.ToString("O"));
        }

        public string GetFormattedMessage(string message, bool excludeTimestamp = false)
        {
            if(excludeTimestamp)
            {
                return message;
            }
            return String.Format(LogFormat, DateTime.UtcNow.ToString("O"), message);
        }

        public string GetFormattedMessage(string format, params object[] args)
        {
            return GetFormattedMessage(String.Format(format, args));
        }

        protected string MessageWithTraceEventType(TraceEventType traceEventType, string message)
        {
            string traceEventTypeString;
            if (!TraceEventTypeStrings.TryGetValue(traceEventType, out traceEventTypeString))
            {
                traceEventTypeString = traceEventType.ToString();
            }
            return String.Format(MessageWithTraceEventTypeFormat, traceEventTypeString, message);
        }

        protected void LogConsoleOnly(TraceEventType traceEventType, string message)
        {
            ConsoleColor currentConsoleForegroundColor = Console.ForegroundColor;
            ConsoleColor traceEventTypeHeaderColor;
            ConsoleColor logMessageColor;
            switch (traceEventType)
            {
                case TraceEventType.Critical:
                case TraceEventType.Error:
                    traceEventTypeHeaderColor = ConsoleColor.Red;
                    break;
                case TraceEventType.Warning:
                    traceEventTypeHeaderColor = ConsoleColor.Yellow;
                    break;
                case TraceEventType.Information:
                    traceEventTypeHeaderColor = ConsoleColor.Cyan;
                    break;
                case TraceEventType.Verbose:
                    traceEventTypeHeaderColor = ConsoleColor.DarkGray;
                    break;
                default:
                    traceEventTypeHeaderColor = ConsoleColor.White;
                    break;
            }

            if (traceEventType < TraceEventType.Information)
            {
                // If TraceEventType is less than Information, that is, if it is Criticial, Error or Warning
                // Use differentiating color on the entire message
                logMessageColor = traceEventTypeHeaderColor;
            }
            else
            {
                logMessageColor = ConsoleColor.DarkGray;
            }

            string fullMessage = MessageWithTraceEventType(traceEventType, message);

            // Set Console foregroundcolor to the color determined for the header
            Console.ForegroundColor = traceEventTypeHeaderColor;
            Console.Write(fullMessage.Substring(0, LogTraceEventTypeHeaderLength));

            // Set Console foregroundcolor to the color determined for the message
            Console.ForegroundColor = logMessageColor;
            Console.WriteLine(fullMessage.Substring(LogTraceEventTypeHeaderLength));

            // Set Console foregroundcolor back to the original color as used by the console
            Console.ForegroundColor = currentConsoleForegroundColor;
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
        public virtual void FlushAllAndEnd(string jobEndMessage)
        {
            // Check AzureBlobJobTraceLogger
            Trace.Listeners.Clear();
        }

        protected virtual void Log(TraceEventType traceEventType, string message)
        {
            LogConsoleOnly(traceEventType, message);
        }

        protected virtual void Log(TraceEventType traceEventType, string format, params object[] args)
        {
            var message = String.Format(format, args);
            LogConsoleOnly(traceEventType, message);
        }

        public override void Write(string message)
        {
            WriteLine(message);
        }

        public override void WriteLine(string message)
        {
            Log(TraceEventType.Verbose, GetFormattedMessage(message));
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            Log(eventType, GetFormattedMessage(message));
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            TraceEvent(eventCache, source, eventType, id, String.Format(format, args));
        }

        public override void Fail(string message)
        {
            Log(TraceEventType.Critical, GetFormattedMessage(message));
        }

        public override void Fail(string message, string detailMessage)
        {
            Fail(message + ". Detailed: " + detailMessage);
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
                    Trace.TraceError(Logger.GetFormattedMessage(GetFormattedEventLog(eventData), excludeTimestamp: true));
                    break;
                case EventLevel.Warning:
                    Trace.TraceWarning(Logger.GetFormattedMessage(GetFormattedEventLog(eventData), excludeTimestamp: true));
                    break;
                case EventLevel.LogAlways:
                case EventLevel.Informational:
                    Trace.TraceInformation(Logger.GetFormattedMessage(GetFormattedEventLog(eventData), excludeTimestamp: true));
                    break;
                case EventLevel.Verbose:
                    Trace.WriteLine(Logger.GetFormattedMessage(GetFormattedEventLog(eventData), excludeTimestamp: true));
                    break;
                default:
                    // DO Nothing
                    break;
            }
        }
    }
}

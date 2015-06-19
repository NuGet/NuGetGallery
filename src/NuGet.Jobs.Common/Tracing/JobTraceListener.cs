// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace NuGet.Jobs
{
    /// <summary>
    /// All the jobs MUST use this trace listener. Since logs from all the jobs get written to the same file
    /// We want to ensure that the logs are prefixed with jobName, startTime and more as needed
    /// </summary>
    public class JobTraceListener
        : TraceListener
    {
        /// <summary>
        /// {0} would be the log prefix. Currently, the prefix is '/&lt;jobName&gt;-&lt;startTime&gt;/'
        /// {1} would be the actual log message
        /// Formatted message would be of the form '/&lt;jobName&gt;-&lt;startTime&gt;//&lt;message&gt;'
        /// </summary>
        private const string _logFormat = "[{0}]: {1}";
        private const int _logTraceEventTypeHeaderLength = 12;
        private static readonly string MessageWithTraceEventTypeFormat = "{0, -" + _logTraceEventTypeHeaderLength + "}{1}";
        private static readonly Dictionary<TraceEventType, string> TraceEventTypeStrings = new Dictionary<TraceEventType, string>
        {
          { TraceEventType.Critical, "[Err]:" },
          { TraceEventType.Error, "[Err]:" },
          { TraceEventType.Information, "[Info]:" },
          { TraceEventType.Verbose, "[Verbose]:" },
          { TraceEventType.Warning, "[Warn]:" },
        };

        public JobTraceListener()
        {
            TraceOutputOptions = TraceOptions.DateTime;
        }

        public string GetFormattedMessage(string message, bool excludeTimestamp = false)
        {
            if (excludeTimestamp)
            {
                return message;
            }
            return string.Format(CultureInfo.InvariantCulture, _logFormat, DateTime.UtcNow.ToString("O"), message);
        }

        protected string MessageWithTraceEventType(TraceEventType traceEventType, string message)
        {
            string traceEventTypeString;
            if (!TraceEventTypeStrings.TryGetValue(traceEventType, out traceEventTypeString))
            {
                traceEventTypeString = traceEventType.ToString();
            }
            return string.Format(CultureInfo.InvariantCulture, MessageWithTraceEventTypeFormat, traceEventTypeString, message);
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
            Console.Write(fullMessage.Substring(0, _logTraceEventTypeHeaderLength));

            // Set Console foregroundcolor to the color determined for the message
            Console.ForegroundColor = logMessageColor;
            Console.WriteLine(fullMessage.Substring(_logTraceEventTypeHeaderLength));

            // Set Console foregroundcolor back to the original color as used by the console
            Console.ForegroundColor = currentConsoleForegroundColor;
        }

        public override void Flush()
        {
            Flush(skipCurrentBatch: false);
        }

        public override void Close()
        {
            // Check AzureBlobJobTraceListener
            Trace.Listeners.Remove(this);
        }

        [Conditional("TRACE")]
        protected virtual void Flush(bool skipCurrentBatch)
        {
            // Check AzureBlobJobTraceListener
        }

        protected virtual void Log(TraceEventType traceEventType, string message)
        {
            LogConsoleOnly(traceEventType, message);
        }

        protected virtual void Log(TraceEventType traceEventType, string format, params object[] args)
        {
            var message = string.Format(CultureInfo.InvariantCulture, format, args);
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
            TraceEvent(eventCache, source, eventType, id, string.Format(CultureInfo.InvariantCulture, format, args));
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
}

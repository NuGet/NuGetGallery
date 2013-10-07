using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NLog;

namespace NuGetGallery.Operations.Infrastructure
{
    public class JobLogEntry
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Message { get; set; }
        public LogLevel Level { get; set; }
        public Exception Exception { get; set; }
        public string Logger { get; set; }
        public JobLogEvent FullEvent { get; set; }
    }

    public class JobLogEvent
    {
        public int SequenceID { get; set; }
        public DateTime TimeStamp { get; set; }
        public LogLevel Level { get; set; }
        public string LoggerName { get; set; }
        public string LoggerShortName { get; set; }
        public string Message { get; set; }
        public string[] Parameters { get; set; }
        public string FormattedMessage { get; set; }
    }
}

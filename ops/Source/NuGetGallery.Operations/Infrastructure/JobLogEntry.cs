using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace NuGetGallery.Operations.Infrastructure
{
    public class JobLogEntry
    {
        public DateTimeOffset Timestamp { get; set; }
        public string Message { get; set; }
        public string Level { get; set; }
        public Exception Exception { get; set; }
        public string Logger { get; set; }
        public LogEventInfo FullEvent { get; set; }
    }
}

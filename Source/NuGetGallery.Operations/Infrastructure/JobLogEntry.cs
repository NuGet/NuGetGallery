using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Operations.Infrastructure
{
    public class JobLogEntry
    {
        public int Index { get; set; }
        public int ThreadId { get; set; }
        public string CallSite { get; set; }
        public DateTime Date { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public JobLogExceptionInfo Exception { get; set; }
    }

    public class JobLogExceptionInfo
    {
        public string Type { get; set; }
        public string Message { get; set; }
        public string Method { get; set; }
        public string StackTrace { get; set; }
    }
}

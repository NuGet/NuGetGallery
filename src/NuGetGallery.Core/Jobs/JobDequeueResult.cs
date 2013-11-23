using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Jobs
{
    public class JobDequeueResult
    {
        public JobRequest Request { get; set; }
        public Exception ParseException { get; set; }
        public string MessageBody { get; set; }
        public bool Success { get { return Request != null; } }

        public JobDequeueResult(JobRequest request)
        {
            Request = request;
        }

        public JobDequeueResult(Exception parseException, string messageBody)
        {
            ParseException = parseException;
            MessageBody = messageBody;
        }
    }
}

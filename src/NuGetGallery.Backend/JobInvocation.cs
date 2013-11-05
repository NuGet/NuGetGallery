using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Backend.Worker
{
    public class JobInvocation
    {
        public JobRequest Request { get; private set; }
        public DateTimeOffset RecievedAt { get; private set; }
        public string Source { get; private set; }

        public JobInvocation(JobRequest request, DateTimeOffset recievedAt, string source)
        {
            Request = request;
            RecievedAt = recievedAt;
            Source = source;
        }
    }
}

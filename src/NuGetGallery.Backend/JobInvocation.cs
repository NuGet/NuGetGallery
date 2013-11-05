using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Backend
{
    public class JobInvocation
    {
        public Guid Id { get; private set; }
        public JobRequest Request { get; private set; }
        public DateTimeOffset RecievedAt { get; private set; }
        public string Source { get; private set; }

        public JobInvocation(Guid id, JobRequest request, DateTimeOffset recievedAt, string source)
        {
            Id = id;
            Request = request;
            RecievedAt = recievedAt;
            Source = source;
        }
    }
}

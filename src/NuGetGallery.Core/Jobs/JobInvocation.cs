using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Jobs
{
    public class JobInvocation
    {
        public Guid Id { get; private set; }
        public JobRequest Request { get; private set; }
        public DateTimeOffset RecievedAt { get; private set; }

        public JobInvocation(Guid id, JobRequest request, DateTimeOffset recievedAt)
        {
            Id = id;
            Request = request;
            RecievedAt = recievedAt;
        }
    }
}

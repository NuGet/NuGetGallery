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
        public bool IsContinuation { get; private set; }

        public JobInvocation(Guid id, JobRequest request, DateTimeOffset recievedAt)
            : this(id, request, recievedAt, false)
        {
        }

        public JobInvocation(Guid id, JobRequest request, DateTimeOffset recievedAt, bool isContinuation)
        {
            Id = id;
            Request = request;
            RecievedAt = recievedAt;
            IsContinuation = isContinuation;
        }
    }
}

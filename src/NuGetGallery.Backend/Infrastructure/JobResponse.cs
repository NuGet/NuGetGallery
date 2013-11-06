using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Backend
{
    public class JobResponse
    {
        public JobInvocation Invocation { get; private set; }
        public JobResult Result { get; private set; }
        public DateTimeOffset CompletedAt { get; private set; }

        public JobResponse(JobInvocation invocation, JobResult result, DateTimeOffset completedAt)
        {
            Invocation = invocation;
            Result = result;
            CompletedAt = completedAt;
        }
    }
}

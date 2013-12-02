using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGet.Services.Jobs
{
    public class JobResponse
    {
        public JobInvocation Invocation { get; private set; }
        public JobResult Result { get; private set; }
        public DateTimeOffset EndedAt { get; private set; }

        public JobResponse(JobInvocation invocation, JobResult result, DateTimeOffset completedAt)
        {
            Invocation = invocation;
            Result = result;
            EndedAt = completedAt;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs
{
    public class JobServiceStatus
    {
        public RunnerStatus RunnerStatus { get; private set; }
        public Guid CurrentInvocationId { get; private set; }
        public JobDescription CurrentJob { get; private set; }

        public JobServiceStatus(RunnerStatus runnerStatus, Guid currentInvocationId, JobDescription currentJob)
        {
            RunnerStatus = runnerStatus;
            CurrentInvocationId = currentInvocationId;
            CurrentJob = currentJob;
        }
    }

    public enum RunnerStatus
    {
        Working,
        Dequeuing,
        Sleeping,
        Dispatching,
        Stopping
    }
}

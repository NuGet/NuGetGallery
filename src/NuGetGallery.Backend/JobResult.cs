using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Backend.Worker
{
    public class JobResult
    {
        public JobStatus Status { get; private set; }
    }

    public enum JobStatus
    {
        Completed,
        Faulted,
        TimedOut,
        WaitingForAsyncCompletion
    }
}

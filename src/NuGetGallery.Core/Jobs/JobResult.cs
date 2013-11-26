using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NuGetGallery.Jobs
{
    public class JobResult
    {
        public JobStatus Status { get; private set; }
        public Exception Exception { get; private set; }
        public JobContinuation Continuation { get; private set; }
        public TimeSpan? RescheduleIn { get; private set; }

        private JobResult(JobStatus status)
        {
            Status = status;
        }

        private JobResult(JobStatus status, TimeSpan rescheduleIn)
            : this(status)
        {
            RescheduleIn = rescheduleIn;
        }

        private JobResult(JobStatus status, Exception exception)
            : this(status)
        {
            Exception = exception;
        }

        private JobResult(JobStatus status, Exception exception, TimeSpan rescheduleIn)
            : this(status, exception)
        {
            RescheduleIn = rescheduleIn;
        }

        private JobResult(JobStatus status, JobContinuation continuation)
            : this(status)
        {
            Continuation = continuation;
        }

        public static JobResult Completed()
        {
            return new JobResult(JobStatus.Completed);
        }

        public static JobResult Completed(TimeSpan rescheduleIn)
        {
            return new JobResult(JobStatus.Completed, rescheduleIn);
        }

        public static JobResult Continuing(JobContinuation continuation)
        {
            return new JobResult(JobStatus.AwaitingContinuation, continuation);
        }

        public static JobResult Faulted(Exception ex)
        {
            return new JobResult(JobStatus.Faulted, ex);
        }

        public static JobResult Faulted(Exception ex, TimeSpan rescheduleIn)
        {
            return new JobResult(JobStatus.Faulted, ex, rescheduleIn);
        }
    }

    public enum JobStatus
    {
        Unspecified = 0,
        Executing,
        Completed,
        Faulted,
        AwaitingContinuation
    }
}

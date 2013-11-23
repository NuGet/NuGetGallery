using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Jobs
{
    public class JobResult : IEquatable<JobResult>
    {
        public JobStatus Status { get; private set; }
        public Exception Exception { get; private set; }
        public JobContinuation Continuation { get; private set; }

        private JobResult(JobStatus status)
        {
            Status = status;
        }

        private JobResult(JobStatus status, Exception exception)
            : this(status)
        {
            Exception = exception;
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

        public static JobResult Continuing(JobContinuation continuation)
        {
            return new JobResult(JobStatus.AwaitingContinuation, continuation);
        }

        public static JobResult Faulted(Exception ex)
        {
            return new JobResult(JobStatus.Faulted, ex);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as JobResult);
        }

        public bool Equals(JobResult other)
        {
            return other != null && other.Status == Status && Equals(Exception, other.Exception);
        }

        public override int GetHashCode()
        {
            // Exception's nullity basically defines the status
            return Exception == null ? 0 : Exception.GetHashCode();
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Backend
{
    public class JobResult : IEquatable<JobResult>
    {
        public JobStatus Status { get; private set; }
        public Exception Exception { get; private set; }

        private JobResult(JobStatus status, Exception exception)
        {
            Status = status;
            Exception = exception;
        }

        public static JobResult Faulted(Exception ex)
        {
            return new JobResult(JobStatus.Faulted, ex);
        }

        public static JobResult Completed()
        {
            return new JobResult(JobStatus.Completed, exception: null);
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
        Completed,
        Faulted
    }
}

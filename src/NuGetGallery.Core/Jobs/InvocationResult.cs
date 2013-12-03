using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NuGet.Services.Jobs
{
    public class InvocationResult
    {
        public InvocationStatus Status { get; private set; }
        public Exception Exception { get; private set; }
        public JobContinuation Continuation { get; private set; }
        public TimeSpan? RescheduleIn { get; private set; }

        private InvocationResult(InvocationStatus status)
        {
            Status = status;
        }

        private InvocationResult(InvocationStatus status, TimeSpan rescheduleIn)
            : this(status)
        {
            RescheduleIn = rescheduleIn;
        }

        private InvocationResult(InvocationStatus status, Exception exception)
            : this(status)
        {
            Exception = exception;
        }

        private InvocationResult(InvocationStatus status, Exception exception, TimeSpan rescheduleIn)
            : this(status, exception)
        {
            RescheduleIn = rescheduleIn;
        }

        private InvocationResult(InvocationStatus status, JobContinuation continuation)
            : this(status)
        {
            Continuation = continuation;
        }

        public static InvocationResult Completed()
        {
            return new InvocationResult(InvocationStatus.Completed);
        }

        public static InvocationResult Completed(TimeSpan rescheduleIn)
        {
            return new InvocationResult(InvocationStatus.Completed, rescheduleIn);
        }

        public static InvocationResult Continuing(JobContinuation continuation)
        {
            return new InvocationResult(InvocationStatus.Suspended, continuation);
        }

        public static InvocationResult Faulted(Exception ex)
        {
            return new InvocationResult(InvocationStatus.Faulted, ex);
        }

        public static InvocationResult Faulted(Exception ex, TimeSpan rescheduleIn)
        {
            return new InvocationResult(InvocationStatus.Faulted, ex, rescheduleIn);
        }
    }
}

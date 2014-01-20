using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using NuGet.Services.Work.Models;

namespace NuGet.Services.Work
{
    public class InvocationResult
    {
        public ExecutionResult Result { get; private set; }
        public Exception Exception { get; private set; }
        public JobContinuation Continuation { get; private set; }
        public TimeSpan? RescheduleIn { get; private set; }

        private InvocationResult(ExecutionResult result, TimeSpan? rescheduleIn, JobContinuation continuation, Exception exception)
        {
            Result = result;
            RescheduleIn = rescheduleIn;
            Continuation = continuation;
            Exception = exception;
            ConsistencyCheck();
        }

        internal InvocationResult(ExecutionResult result) : this(result, null, null, null) { }
        internal InvocationResult(ExecutionResult result, TimeSpan rescheduleIn) : this(result, rescheduleIn, null, null) { }
        internal InvocationResult(ExecutionResult result, Exception exception) : this(result, null, null, exception) { }
        internal InvocationResult(ExecutionResult result, Exception exception, TimeSpan rescheduleIn) : this(result, rescheduleIn, null, exception) { }
        internal InvocationResult(ExecutionResult result, JobContinuation continuation) : this(result, null, continuation, null) {}

        public static InvocationResult Completed()
        {
            return new InvocationResult(ExecutionResult.Completed);
        }

        public static InvocationResult Completed(TimeSpan rescheduleIn)
        {
            return new InvocationResult(ExecutionResult.Completed, rescheduleIn);
        }

        public static InvocationResult Suspended(JobContinuation continuation)
        {
            return new InvocationResult(ExecutionResult.Incomplete, continuation);
        }

        public static InvocationResult Faulted(Exception ex)
        {
            return new InvocationResult(ExecutionResult.Faulted, ex);
        }

        public static InvocationResult Faulted(Exception ex, TimeSpan rescheduleIn)
        {
            return new InvocationResult(ExecutionResult.Faulted, ex, rescheduleIn);
        }

        [Conditional("DEBUG")]
        private void ConsistencyCheck()
        {
            // Checks that we don't have missing or invalid fields
            switch (Result)
            {
                case ExecutionResult.Incomplete:
                    Debug.Assert(Continuation != null, String.Format(CultureInfo.CurrentCulture, Strings.InvocationResult_ResultMustHaveContinuation, Result));
                    Debug.Assert(Exception == null, String.Format(CultureInfo.CurrentCulture, Strings.InvocationResult_ResultMustNotHaveException, Result));
                    Debug.Assert(RescheduleIn == null, String.Format(CultureInfo.CurrentCulture, Strings.InvocationResult_ResultMustNotHaveRescheduleIn, Result));
                    break;
                case ExecutionResult.Completed:
                    Debug.Assert(Continuation == null, String.Format(CultureInfo.CurrentCulture, Strings.InvocationResult_ResultMustNotHaveContinuation, Result));
                    Debug.Assert(Exception == null, String.Format(CultureInfo.CurrentCulture, Strings.InvocationResult_ResultMustNotHaveException, Result));
                    break;
                case ExecutionResult.Faulted:
                    Debug.Assert(Continuation == null, String.Format(CultureInfo.CurrentCulture, Strings.InvocationResult_ResultMustNotHaveContinuation, Result));
                    Debug.Assert(Exception != null, String.Format(CultureInfo.CurrentCulture, Strings.InvocationResult_ResultMustHaveException, Result));
                    break;
                case ExecutionResult.Crashed:
                    Debug.Assert(Continuation == null, String.Format(CultureInfo.CurrentCulture, Strings.InvocationResult_ResultMustNotHaveContinuation, Result));
                    Debug.Assert(Exception != null, String.Format(CultureInfo.CurrentCulture, Strings.InvocationResult_ResultMustHaveException, Result));
                    Debug.Assert(RescheduleIn == null, String.Format(CultureInfo.CurrentCulture, Strings.InvocationResult_ResultMustNotHaveRescheduleIn, Result));
                    break;
                case ExecutionResult.Aborted:
                    Debug.Assert(Continuation == null, String.Format(CultureInfo.CurrentCulture, Strings.InvocationResult_ResultMustNotHaveContinuation, Result));
                    Debug.Assert(Exception == null, String.Format(CultureInfo.CurrentCulture, Strings.InvocationResult_ResultMustNotHaveException, Result));
                    break;
                default:
                    break;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs
{
    public enum InvocationStatus
    {
        /// <summary>
        /// Indicates that the status of this invocation has not been set
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Indicates that the invocation is being placed on the invocation queue
        /// </summary>
        Queuing,

        /// <summary>
        /// Indicates that the invocation has been placed on the invocation queue
        /// </summary>
        Queued,

        /// <summary>
        /// Indicates that the invocation has been dequeued from the data store
        /// </summary>
        Dequeued,

        /// <summary>
        /// Indicates that the invocation has been received by a worker and is executing
        /// </summary>
        Executing,

        /// <summary>
        /// Indicates that the invocation has been executed
        /// </summary>
        Executed,

        /// <summary>
        /// Indicates that the invocation has been cancelled by an outside agent and should not be invoked.
        /// </summary>
        Cancelled
    }

    public enum ExecutionResult
    {
        /// <summary>
        /// Indicates that the invocation has not yet been executed
        /// </summary>
        Incomplete = 0,

        /// <summary>
        /// Indicates that the invocation has been suspended and is being queued for another invocation
        /// </summary>
        Suspended,

        /// <summary>
        /// Indicates that the invocation has completed successfully
        /// </summary>
        /// <remarks>Invocations in this state can be cleaned up as they will not be needed again</remarks>
        Completed,

        /// <summary>
        /// Indicates that the invocation has completed with an error
        /// </summary>
        /// <remarks>Invocations in this state can be cleaned up as they will not be needed again (other than for debugging purposes)</remarks>
        Faulted,

        /// <summary>
        /// Indicates that the a fatal error occurred during the execution of this invocation
        /// </summary>
        /// <remarks>Invocations in this state can be cleaned up as they will not be needed again (other than for debugging purposes)</remarks>
        Crashed,
        
        /// <summary>
        /// Indicates that the invocation failed due to the worker running it being shutdown
        /// </summary>
        /// <remarks>Invocations in this state can be cleaned up as they will not be needed again (other than for debugging/retry purposes)</remarks>
        Aborted
    }
}

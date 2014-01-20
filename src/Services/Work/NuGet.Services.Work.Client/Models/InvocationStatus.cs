using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Models
{
    /// <summary>
    /// Indicates the status of the invocation
    /// </summary>
    /// <remarks>NOTE: This is stored in an INT column in a database, DO NOT adjust the numbers assigned to each value!</remarks>
    public enum InvocationStatus
    {
        /// <summary>
        /// Indicates that the status of this invocation has not been set
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// Indicates that the invocation has been placed on the invocation queue
        /// </summary>
        Queued = 1,

        /// <summary>
        /// Indicates that the invocation has been dequeued from the data store
        /// </summary>
        Dequeued = 2,

        /// <summary>
        /// Indicates that the invocation has been received by a worker and is executing
        /// </summary>
        Executing = 3,

        /// <summary>
        /// Indicates that the invocation has been executed
        /// </summary>
        Executed = 4,

        /// <summary>
        /// Indicates that the invocation has been cancelled by an outside agent and should not be invoked.
        /// </summary>
        Cancelled = 5,

        /// <summary>
        /// Indicates that the invocation has been suspended and is being queued for another invocation
        /// </summary>
        Suspended = 6
    }

    /// <summary>
    /// Indicates the result of the invocation
    /// </summary>
    /// <remarks>NOTE: This is stored in an INT column in a database, DO NOT adjust the numbers assigned to each value!</remarks>
    public enum ExecutionResult
    {
        /// <summary>
        /// Indicates that the invocation has not yet been executed
        /// </summary>
        Incomplete = 0,

        /// <summary>
        /// Indicates that the invocation has completed successfully
        /// </summary>
        /// <remarks>Invocations in this state can be cleaned up as they will not be needed again</remarks>
        Completed = 1,

        /// <summary>
        /// Indicates that the invocation has completed with an error
        /// </summary>
        /// <remarks>Invocations in this state can be cleaned up as they will not be needed again (other than for debugging purposes)</remarks>
        Faulted = 2,

        /// <summary>
        /// Indicates that the a fatal error occurred during the execution of this invocation
        /// </summary>
        /// <remarks>Invocations in this state can be cleaned up as they will not be needed again (other than for debugging purposes)</remarks>
        Crashed = 3,

        /// <summary>
        /// Indicates that the invocation failed due to the worker running it being shutdown
        /// </summary>
        /// <remarks>Invocations in this state can be cleaned up as they will not be needed again (other than for debugging/retry purposes)</remarks>
        Aborted = 4
    }
}

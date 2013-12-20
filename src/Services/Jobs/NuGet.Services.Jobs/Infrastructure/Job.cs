using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Jobs.Helpers;

namespace NuGet.Services.Jobs
{
    public abstract class JobBase<TEventSource> : JobBase
        where TEventSource : EventSource
    {
        private TEventSource _log = EventSourceInstanceManager.Get<TEventSource>();

        public TEventSource Log { get { return _log; } }

        public override EventSource GetEventSource()
        {
            return Log;
        }
    }

    public abstract class Job<TEventSource> : JobBase<TEventSource>
        where TEventSource : EventSource
    {
        protected internal override async Task<InvocationResult> Invoke()
        {
            try
            {
                await Execute();
                return InvocationResult.Completed();
            }
            catch (Exception ex)
            {
                return InvocationResult.Faulted(ex);
            }
        }

        protected internal abstract Task Execute();
    }

    public abstract class RepeatingJob<TEventSource> : JobBase<TEventSource> 
        where TEventSource: EventSource
    {
        /// <summary>
        /// The amount of time to wait before invoking again, when the job goes idle
        /// </summary>
        public abstract TimeSpan WaitPeriod { get; }

        protected internal override async Task<InvocationResult> Invoke()
        {
            // Invoke the job. When it returns, it means there's no more data to process
            // So go to sleep until the wait period elapses
            try
            {
                await Execute();
                return InvocationResult.Completed(WaitPeriod);
            }
            catch (Exception ex)
            {
                return InvocationResult.Faulted(ex, WaitPeriod);
            }
        }

        protected internal abstract Task Execute();
    }

    public interface IAsyncJob
    {
        Task<InvocationResult> InvokeContinuation(InvocationContext context);
    }

    public abstract class AsyncJob<TEventSource> : JobBase<TEventSource>, IAsyncJob
        where TEventSource : EventSource
    {
        protected internal override Task<InvocationResult> Invoke()
        {
            return InvokeCore(() => Execute());
        }

        public Task<InvocationResult> InvokeContinuation(InvocationContext context)
        {
            InvocationResult result = BindContext(context);
            if (result != null)
            {
                return Task.FromResult(result);
            }

            return InvokeCore(() => Resume());
        }

        protected internal abstract Task<JobContinuation> Execute();
        protected internal abstract Task<JobContinuation> Resume();

        protected virtual JobContinuation Suspend(TimeSpan waitPeriod, Dictionary<string, string> parameters)
        {
            // TODO: Using a [ContinuationState] or similar attribute, allow the job author
            //  to just mark properties that they want carried over to the continuation.
            //  Then just build the 'parameters' dictionary by reading those values.
            return new JobContinuation(waitPeriod, parameters);
        }

        protected virtual JobContinuation Complete()
        {
            return null;
        }

        private async Task<InvocationResult> InvokeCore(Func<Task<JobContinuation>> invoker)
        {

            try
            {
                var continuation = await invoker();
                if (continuation != null)
                {
                    return InvocationResult.Suspended(continuation);
                }
                else
                {
                    return InvocationResult.Completed();
                }
            }
            catch (Exception ex)
            {
                return InvocationResult.Faulted(ex);
            }
        }
    }
}

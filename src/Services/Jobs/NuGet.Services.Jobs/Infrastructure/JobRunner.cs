using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGet.Services.Configuration;
using NuGet.Services.Jobs.Configuration;
using NuGet.Services.Jobs.Monitoring;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;

namespace NuGet.Services.Jobs
{
    public class JobRunner
    {
        public static readonly TimeSpan DefaultInvisibilityPeriod = TimeSpan.FromSeconds(30);

        private TimeSpan _pollInterval;
        private RunnerStatus _status;

        protected Clock Clock { get; set; }
        protected InvocationQueue Queue { get; set; }
        protected JobDispatcher Dispatcher { get; set; }
        protected StorageHub Storage { get; set; }

        public RunnerStatus Status
        {
            get { return _status; }
            set { _status = value; OnHeartbeat(EventArgs.Empty); }
        }
        
        public event EventHandler Heartbeat;

        protected JobRunner(TimeSpan pollInterval)
        {
            _status = RunnerStatus.Working;
            _pollInterval = pollInterval;
        }

        public JobRunner(JobDispatcher dispatcher, InvocationQueue queue, ConfigurationHub config, StorageHub storage, Clock clock)
            : this(config.GetSection<QueueConfiguration>().PollInterval)
        {
            Dispatcher = dispatcher;
            Queue = queue;
            Clock = clock;
            Storage = storage;
        }

        public virtual async Task Run(CancellationToken cancelToken)
        {
            JobsServiceEventSource.Log.DispatchLoopStarted();
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    InvocationRequest request = null;
                    try
                    {
                        Status = RunnerStatus.Dequeuing;
                        request = await Queue.Dequeue(DefaultInvisibilityPeriod, cancelToken);
                        Status = RunnerStatus.Working;
                    }
                    catch (Exception ex)
                    {
                        JobsServiceEventSource.Log.ErrorRetrievingInvocation(ex);
                    }
                    
                    // Check Cancellation
                    if (cancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (request == null)
                    {
                        Status = RunnerStatus.Sleeping;
                        JobsServiceEventSource.Log.DispatchLoopWaiting(_pollInterval);
                        await Clock.Delay(_pollInterval, cancelToken);
                        JobsServiceEventSource.Log.DispatchLoopResumed();
                        Status = RunnerStatus.Working;
                    }
                    else if (request.Invocation.Status == InvocationStatus.Cancelled)
                    {
                        // Job was cancelled by the user, so just continue.
                        JobsServiceEventSource.Log.Cancelled(request.Invocation);
                    }
                    else
                    {
                        Status = RunnerStatus.Dispatching;
                        await Dispatch(request, cancelToken);
                        Status = RunnerStatus.Working;
                    }
                }
                Status = RunnerStatus.Stopping;
            }
            catch (Exception ex)
            {
                JobsServiceEventSource.Log.DispatchLoopError(ex);
                throw;
            }
            JobsServiceEventSource.Log.DispatchLoopEnded();
        }

        protected virtual void OnHeartbeat(EventArgs args)
        {
            var handler = Heartbeat;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        protected internal virtual async Task Dispatch(InvocationRequest request, CancellationToken cancelToken)
        {
            InvocationContext.SetCurrentInvocationId(request.Invocation.Id);
            
            if (request.Invocation.Continuation)
            {
                InvocationEventSource.Log.Resumed();
            }
            else
            {
                InvocationEventSource.Log.Started();
            }
            
            // Record that we are executing the job
            request.Invocation.LastDequeuedAt = Clock.UtcNow;
            request.Invocation.Status = InvocationStatus.Executing;
            await Queue.Update(request.Invocation);

            if (cancelToken.IsCancellationRequested)
            {
                return;
            }

            // Create the request.Invocation context and start capturing the logs
            var capture = new InvocationLogCapture(request.Invocation, Storage, Path.Combine(Path.GetTempPath(), "InvocationLogs"));
            var context = new InvocationContext(request, Queue, capture);
            await capture.Start();

            InvocationResult result = null;
            try
            {
                result = await Dispatcher.Dispatch(context);

                // TODO: If response.Continuation != null, enqueue continuation
                switch (result.Status)
                {
                    case InvocationStatus.Completed:
                        InvocationEventSource.Log.Succeeded(result);
                        break;
                    case InvocationStatus.Faulted:
                        InvocationEventSource.Log.Faulted(result);
                        break;
                    case InvocationStatus.Suspended:
                        InvocationEventSource.Log.Suspended(result);
                        break;
                    default:
                        InvocationEventSource.Log.UnknownStatus(result);
                        break;
                }

                if (request.Message.NextVisibleTime.HasValue && request.Message.NextVisibleTime.Value < DateTimeOffset.UtcNow)
                {
                    InvocationEventSource.Log.InvocationTookTooLong(request);
                }

                // If dispatch throws, we don't delete the message
                // NOTE: If the JOB throws, the dispatcher should catch it and return the error in the response
                // Thus the request is considered "handled"
                await Queue.Acknowledge(request);
            }
            catch (Exception ex)
            {
                InvocationEventSource.Log.DispatchError(ex);
                result = InvocationResult.Crashed(ex);
            }

            // Stop capturing and set the log url
            var logBlob = await capture.End();
            request.Invocation.Status = result.Status;
            if (logBlob != null)
            {
                request.Invocation.LogUrl = logBlob.Uri.AbsoluteUri;
            }

            // If we are in a termination state, report that
            if (result.Status == InvocationStatus.Completed || 
                result.Status == InvocationStatus.Faulted)
            {
                request.Invocation.CompletedAt = DateTimeOffset.UtcNow;
                InvocationEventSource.Log.Ended();
            }

            // If we faulted, report the error
            if (result.Status == InvocationStatus.Faulted)
            {
                request.Invocation.StatusMessage = result.Exception.ToString();
            }

            // Update the status of the request.Invocation
            await Queue.Update(request.Invocation);
            
            // If we're suspended, queue a continuation
            if (result.Status == InvocationStatus.Suspended)
            {
                Debug.Assert(result.Continuation != null);
                await EnqueueContinuation(request.Invocation, result);
            }
            
            // If we've completed and there's a repeat, queue the repeat
            if (result.Status == InvocationStatus.Completed && result.RescheduleIn != null)
            {
                // Rescheule it to run again
                await EnqueueRepeat(request.Invocation, result);
            }
        }

        private Task EnqueueContinuation(Invocation continuation, InvocationResult result)
        {
            var invocation = new Invocation(
                continuation.Id,
                continuation.Job,
                Constants.Source_AsyncContinuation,
                continuation.Payload)
                {
                    Continuation = true,
                    LastSuspendedAt = DateTimeOffset.UtcNow,
                    EstimatedContinueAt = DateTimeOffset.UtcNow + result.Continuation.WaitPeriod
                };
            return Queue.Enqueue(continuation, result.Continuation.WaitPeriod);
        }

        private Task EnqueueRepeat(Invocation repeat, InvocationResult result)
        {
            var invocation = new Invocation(
                Guid.NewGuid(),
                repeat.Job,
                Constants.Source_RepeatingJob,
                repeat.Payload)
                {
                    EstimatedNextVisibleTime = DateTimeOffset.UtcNow + result.RescheduleIn.Value
                };
            return Queue.Enqueue(invocation, result.RescheduleIn.Value);
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
}

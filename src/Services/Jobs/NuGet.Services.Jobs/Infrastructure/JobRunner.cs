using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGet.Services.Jobs.Monitoring;

namespace NuGet.Services.Jobs
{
    public class JobRunner
    {
        public static readonly TimeSpan DefaultInvisibilityPeriod = TimeSpan.FromSeconds(30);

        private JobDispatcher _dispatcher;
        private InvocationQueue _queue;
        private ServiceConfiguration _config;
        private BackendMonitoringHub _monitoring;

        public JobRunner(JobDispatcher dispatcher, ServiceConfiguration config, BackendMonitoringHub monitoring)
        {
            _dispatcher = dispatcher;
            _config = config;
            _monitoring = monitoring;

            _queue = new InvocationQueue(config.InstanceId, config.Storage);
            
            // Register jobs
            foreach (var job in dispatcher.Jobs)
            {
                monitoring.RegisterJob(job);
            }
        }

        public async Task Run(CancellationToken cancelToken)
        {
            JobsServiceEventSource.Log.DispatchLoopStarted();
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    InvocationRequest request = null;
                    try
                    {
                        request = await _queue.Dequeue(DefaultInvisibilityPeriod, cancelToken);
                    }
                    catch (Exception ex)
                    {
                        JobsServiceEventSource.Log.ErrorRetrievingInvocation(ex);
                    }
                    if (request == null)
                    {
                        JobsServiceEventSource.Log.DispatchLoopWaiting(_config.QueuePollInterval);
                        await Task.Delay(_config.QueuePollInterval);
                        JobsServiceEventSource.Log.DispatchLoopResumed();
                    }
                    else
                    {
                        await Dispatch(request);
                    }
                }
            }
            catch (Exception ex)
            {
                JobsServiceEventSource.Log.DispatchLoopError(ex);
            }
            JobsServiceEventSource.Log.DispatchLoopEnded();
        }

        private async Task Dispatch(InvocationRequest request)
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
            request.Invocation.LastDequeuedAt = DateTimeOffset.UtcNow;
            request.Invocation.Status = InvocationStatus.Executing;
            await _queue.Update(request.Invocation);

            // Create the request.Invocation context and start capturing the logs
            var capture = new InvocationLogCapture(request.Invocation, _monitoring);
            var context = new InvocationContext(request, _queue, _config, capture);
            await capture.Start();

            InvocationResult result = null;
            try
            {
                result = await _dispatcher.Dispatch(context);

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
                await _queue.Acknowledge(request);
            }
            catch (Exception ex)
            {
                InvocationEventSource.Log.DispatchError(ex);
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
                request.Invocation.Exception = result.Exception.ToString();
            }

            // Update the status of the request.Invocation
            await _queue.Update(request.Invocation);
            
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
            return _queue.Enqueue(continuation, result.Continuation.WaitPeriod);
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
            return _queue.Enqueue(invocation, result.RescheduleIn.Value);
        }
    }
}

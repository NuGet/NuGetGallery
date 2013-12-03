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
                    Invocation invocation;
                    try
                    {
                        invocation = await _queue.Dequeue(DefaultInvisibilityPeriod, cancelToken);
                    }
                    catch (Exception ex)
                    {
                        JobsServiceEventSource.Log.ErrorRetrievingInvocation(ex);
                    }
                    if (invocation == null)
                    {
                        JobsServiceEventSource.Log.DispatchLoopWaiting(_config.QueuePollInterval);
                        await Task.Delay(_config.QueuePollInterval);
                        JobsServiceEventSource.Log.DispatchLoopResumed();
                    }
                    else
                    {
                        await Dispatch(invocation);
                    }
                }
            }
            catch (Exception ex)
            {
                JobsServiceEventSource.Log.DispatchLoopError(ex);
            }
            JobsServiceEventSource.Log.DispatchLoopEnded();
        }

        private async Task Dispatch(Invocation invocation)
        {
            InvocationContext.SetCurrentInvocationId(invocation.Id);
            
            if (isContinuation)
            {
                InvocationEventSource.Log.Resumed();
            }
            else
            {
                InvocationEventSource.Log.Started();
            }
            
            // Record that we are executing the job
            invocation.LastDequeuedAt = DateTimeOffset.UtcNow;
            invocation.Status = InvocationStatus.Executing;
            await _queue.Update(invocation);

            // Create the invocation context and start capturing the logs
            var context = new InvocationContext(invocation, _queue, _config);
            var capture = new InvocationLogCapture(invocation, _config.Storage.Primary.Blobs);
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

                if (request.ExpiresAt.HasValue && DateTimeOffset.UtcNow > request.ExpiresAt.Value)
                {
                    InvocationEventSource.Log.RequestExpired(request);
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
            invocation.Status = result.Status;
            if (logBlob != null)
            {
                invocation.LogUrl = logBlob.Uri.AbsoluteUri;
            }

            // If we are in a termination state, report that
            if (result.Status == InvocationStatus.Completed || 
                result.Status == InvocationStatus.Faulted)
            {
                invocation.CompletedAt = DateTimeOffset.UtcNow;
                InvocationEventSource.Log.Ended();
            }

            // If we faulted, report the error
            if (result.Status == InvocationStatus.Faulted)
            {
                invocation.Exception = result.Exception.ToString();
            }

            // Update the status of the invocation
            await _queue.Update(invocation);
            
            // If we're suspended, queue a continuation
            if (result.Status == InvocationStatus.Suspended)
            {
                invocation.LastSuspendedAt = DateTimeOffset.UtcNow;
                invocation.EstimatedContinueAt = DateTimeOffset.UtcNow + result.Continuation.WaitPeriod;
                Debug.Assert(result.Continuation != null);
                await EnqueueContinuation(result);
            }
            
            // If we've completed and there's a repeat, queue the repeat
            if (result.Status == InvocationStatus.Completed && result.RescheduleIn != null)
            {
                invocation.EstimatedReinvokeAt = DateTimeOffset.UtcNow + result.RescheduleIn.Value;
                // Rescheule it to run again
                await EnqueueRepeat(result);
            }
        }

        private Task EnqueueContinuation(Invocation continuation, InvocationResult result)
        {
            continuation.Source = Constants.Source_AsyncContinuation,
            
            
            var req = new JobRequest(
                response.Invocation.Request.Job,
                Constants.Source_AsyncContinuation,
                response.Result.Continuation.Parameters,
                response.Invocation.Id);
            return _queue.Enqueue(req, response.Result.Continuation.WaitPeriod);
        }

        private Task EnqueueRepeat(Invocation repeat)
        {
            var req = new JobRequest(
                response.Invocation.Request.Job,
                Constants.Source_RepeatingJob,
                response.Invocation.Request.Parameters);
            return _queue.Enqueue(req, response.Result.RescheduleIn.Value);
        }
    }
}

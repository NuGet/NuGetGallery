using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGetGallery.Backend.Monitoring;
using NuGetGallery.Jobs;

namespace NuGetGallery.Backend
{
    public class JobRunner
    {
        public static readonly TimeSpan DefaultInvisibilityPeriod = TimeSpan.FromSeconds(30);

        private JobDispatcher _dispatcher;
        private JobRequestQueue _queue;
        private BackendConfiguration _config;
        private BackendMonitoringHub _monitoring;

        public JobRunner(JobDispatcher dispatcher, BackendConfiguration config, BackendMonitoringHub monitoring)
        {
            _dispatcher = dispatcher;
            _config = config;
            _monitoring = monitoring;

            _queue = JobRequestQueue.WithDefaultName(config.PrimaryStorage);
            
            // Register jobs
            foreach (Job job in dispatcher.Jobs)
            {
                monitoring.RegisterJob(job);
            }
        }

        public async Task Run(CancellationToken cancelToken)
        {
            WorkerEventSource.Log.DispatchLoopStarted();
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    JobDequeueResult dequeued = await _queue.Dequeue(DefaultInvisibilityPeriod, cancelToken);
                    if (dequeued == null)
                    {
                        WorkerEventSource.Log.DispatchLoopWaiting(_config.QueuePollInterval);
                        await Task.Delay(_config.QueuePollInterval);
                        WorkerEventSource.Log.DispatchLoopResumed();
                    }
                    else if (!dequeued.Success)
                    {
                        WorkerEventSource.Log.InvalidQueueMessage(dequeued.MessageBody, dequeued.ParseException);
                    }
                    else
                    {
                        Debug.Assert(dequeued.Request.Message != null); // Since we dequeued, there'd better be a CloudQueueMessage.
                        await Dispatch(dequeued.Request);
                    }
                }
            }
            catch (Exception ex)
            {
                WorkerEventSource.Log.DispatchLoopError(ex);
            }
            WorkerEventSource.Log.DispatchLoopEnded();
        }

        private async Task Dispatch(JobRequest request)
        {
            // Construct an invocation. From here on, we're in the context of this invocation.
            var invocation = new JobInvocation(Guid.NewGuid(), request, DateTimeOffset.UtcNow);
            var log = new InvocationEventSource(invocation.Id);
            
            var context = _monitoring.BeginInvocation(invocation, log);
            log.Started();

            JobResponse response = null;
            try
            {
                response = await _dispatcher.Dispatch(invocation, log, context);

                if (response.Result.Status != JobStatus.Faulted)
                {
                    log.Succeeded(response);
                }
                else
                {
                    log.Faulted(response);
                }

                if (request.ExpiresAt.HasValue && DateTimeOffset.UtcNow > request.ExpiresAt.Value)
                {
                    log.RequestExpired(request);
                }

                // If dispatch throws, we don't delete the message
                // NOTE: If the JOB throws, the dispatcher should catch it and return the error in the response
                // Thus the request is considered "handled"
                await _queue.Acknowledge(request);
            }
            catch (Exception ex)
            {
                log.DispatchError(ex);
            }

            log.Ended();
            await context.End(response == null ? null : response.Result);
        }
    }
}

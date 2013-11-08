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
            WorkerEventSource.Log.WorkerDispatching();
            while (!cancelToken.IsCancellationRequested)
            {
                var response = await DispatchOne(cancelToken);
                if (response != null)
                {
                    WorkerEventSource.Log.JobExecuted(response);
                }
                else
                {
                    WorkerEventSource.Log.QueueEmpty(_config.QueuePollInterval);
                    await Task.Delay(_config.QueuePollInterval);
                }
            }
        }

        private async Task<JobResponse> DispatchOne(CancellationToken cancelToken)
        {
            var request = await _queue.Dequeue(DefaultInvisibilityPeriod, cancelToken);
            if (request == null)
            {
                return null;
            }
            Debug.Assert(request.Message != null); // Since we dequeued, there'd better be a CloudQueueMessage.
            WorkerEventSource.Log.RequestReceived(request.Id, request.InsertionTime);

            var invocation = new JobInvocation(Guid.NewGuid(), request, DateTimeOffset.UtcNow);
            try
            {
                JobResponse response = await _dispatcher.Dispatch(invocation);

                if (request.ExpiresAt.HasValue && DateTimeOffset.UtcNow > request.ExpiresAt.Value)
                {
                    WorkerEventSource.Log.JobRequestExpired(request, request.Id, DateTimeOffset.UtcNow - request.ExpiresAt.Value);
                }

                // If dispatch throws, we don't delete the message
                // NOTE: If the JOB throws, the dispatcher should catch it and return the error in the response
                // Thus the request is considered "handled"
                await _queue.Acknowledge(request);

                return response;
            }
            catch(Exception ex)
            {
                WorkerEventSource.Log.DispatchError(invocation, ex);
                return null;
            }
        }
    }
}

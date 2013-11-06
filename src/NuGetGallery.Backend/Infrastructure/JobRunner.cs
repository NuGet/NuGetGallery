using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;
using NuGetGallery.Backend.Tracing;

namespace NuGetGallery.Backend
{
    public class JobRunner
    {
        private JobDispatcher _dispatcher;
        private CloudQueue _queue;
        private BackendConfiguration _config;
        private DiagnosticsManager _diagnostics;

        public JobRunner(JobDispatcher dispatcher, BackendConfiguration config, DiagnosticsManager diagnostics)
        {
            _dispatcher = dispatcher;
            _config = config;
            _diagnostics = diagnostics;

            var queueClient = config.PrimaryStorage.CreateCloudQueueClient();
            _queue = queueClient.GetQueueReference("nugetworkerrequests");
        }

        public async Task Run(CancellationToken cancelToken)
        {
            await _queue.CreateIfNotExistsAsync();

            WorkerEventSource.Log.WorkerDispatching();
            while (!cancelToken.IsCancellationRequested)
            {
                var response = await DispatchOne(cancelToken);
                if (response != null)
                {
                    WorkerEventSource.Log.JobExecuted(response);
                    await _diagnostics.ReportJobResponse(response);
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
            var message = await _queue.GetMessageAsync(cancelToken);
            if (message == null)
            {
                return null;
            }
            WorkerEventSource.Log.RequestReceived(message.Id, message.InsertionTime);

            string name;
            var parameters = new Dictionary<string, string>();
            try
            {
                // Parse the message
                JObject parsed;
                try
                {
                    parsed = JObject.Parse(message.AsString);
                }
                catch (Exception ex)
                {
                    WorkerEventSource.Log.InvalidQueueMessage(message.AsString, ex);
                    return null;
                }

                var nameProp = parsed.Property("name");
                if (nameProp == null)
                {
                    WorkerEventSource.Log.InvalidQueueMessage(message.AsString, "Missing 'name' property");
                    return null;
                }
                else if (nameProp.Value.Type != JTokenType.String)
                {
                    WorkerEventSource.Log.InvalidQueueMessage(message.AsString, "'name' property must have string value");
                    return null;
                }
                name = nameProp.Value.Value<string>();

                var parametersProp = parsed.Property("parameters");
                if (parametersProp != null)
                {
                    if (parametersProp.Value.Type != JTokenType.Object)
                    {
                        WorkerEventSource.Log.InvalidQueueMessage(message.AsString, "'parameters' must be a JSON object");
                        return null;
                    }
                    foreach (var prop in ((JObject)parametersProp.Value).Properties())
                    {
                        parameters[prop.Name] = prop.Value.Value<string>();
                    }
                }
            }
            catch (Exception ex)
            {
                WorkerEventSource.Log.MessageParseError(message.AsString, ex);
                return null;
            }

            JobRequest req = new JobRequest(name, parameters);
            
            try
            {
                JobResponse response = _dispatcher.Dispatch(req);

                if (message.ExpirationTime.HasValue && DateTimeOffset.UtcNow > message.ExpirationTime.Value)
                {
                    WorkerEventSource.Log.JobRequestExpired(req, message.Id, DateTimeOffset.UtcNow - message.ExpirationTime.Value);
                }

                // If dispatch throws, we don't delete the message
                // NOTE: If the JOB throws, the dispatcher should catch it and return the error in the response
                // Thus the request is considered "handled"
                await _queue.DeleteMessageAsync(message);

                return response;
            }
            catch(Exception ex)
            {
                WorkerEventSource.Log.DispatchError(req, ex);
                return null;
            }
        }
    }
}

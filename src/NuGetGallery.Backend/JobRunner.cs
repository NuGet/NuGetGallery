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
        private DiagnosticsManager _diagnostics;

        public JobRunner(JobDispatcher dispatcher, CloudQueue queue, DiagnosticsManager diagnostics)
        {
            _dispatcher = dispatcher;
            _queue = queue;
            _diagnostics = diagnostics;
        }

        public async Task Run(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                var response = await DispatchOne(cancelToken);
                if (response != null)
                {
                    await _diagnostics.ReportJobResponse(response);
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

            // Parse the message
            JObject parsed;
            try
            {
                parsed = JObject.Parse(message.AsString);
            }
            catch (Exception ex)
            {
                WorkerEventSource.Log.InvalidQueueMessage(message.AsString, ex.ToString(), ex.StackTrace);
                return null;
            }

            var nameProp = parsed.Property("name");
            if (nameProp == null)
            {
                WorkerEventSource.Log.InvalidQueueMessage(message.AsString, "Missing 'name' property", "");
                return null;
            } else if(nameProp.Value.Type != JTokenType.String) {
                WorkerEventSource.Log.InvalidQueueMessage(message.AsString, "'name' property must have string value", "");
                return null;
            }

            var parametersProp = parsed.Property("parameters");
            var parameters = new Dictionary<string, string>();
            if (parametersProp != null)
            {
                if (parametersProp.Value.Type != JTokenType.Object)
                {
                    WorkerEventSource.Log.InvalidQueueMessage(message.AsString, "'parameters' must be a JSON object", "");
                    return null;
                }
                foreach (var prop in ((JObject)parametersProp.Value).Properties())
                {
                    parameters[prop.Name] = prop.Value<string>();
                }
            }

            JobRequest req = new JobRequest(nameProp.Value<string>(), parameters);
            return _dispatcher.Dispatch(req);
        }
    }
}

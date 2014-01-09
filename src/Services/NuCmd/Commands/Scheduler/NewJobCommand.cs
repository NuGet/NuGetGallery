using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Scheduler.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NuGet.Services.Work;
using NuGet.Services.Work.Models;
using PowerArgs;

namespace NuCmd.Commands.Scheduler
{
    [Description("Creates a new job in the scheduler")]
    public class NewJobCommand : SchedulerCommandBase
    {
        [ArgShortcut("cs")]
        [ArgDescription("Specifies the scheduler service for the collection. Defaults to the standard one for this environment (nuget-[environment]-0-scheduler)")]
        public string CloudService { get; set; }

        [ArgRequired]
        [ArgPosition(0)]
        [ArgShortcut("c")]
        [ArgDescription("The collection in which to put the job")]
        public string Collection { get; set; }

        [ArgRequired]
        [ArgPosition(1)]
        [ArgShortcut("j")]
        [ArgDescription("The job to invoke")]
        public string Job { get; set; }

        [ArgRequired]
        [ArgPosition(2)]
        [ArgShortcut("i")]
        [ArgDescription("The name of the job instance to create")]
        public string InstanceName { get; set; }

        [ArgRequired()]
        [ArgShortcut("pass")]
        [ArgDescription("The password for the work service")]
        public string Password { get; set; }

        [ArgShortcut("p")]
        [ArgDescription("The JSON dictionary payload to provide to the job")]
        public string Payload { get; set; }

        [ArgShortcut("ep")]
        [ArgDescription("A base64-encoded UTF8 payload string to use. Designed for command-line piping")]
        public string EncodedPayload { get; set; }

        [ArgShortcut("url")]
        [ArgDescription("The URI to the root of the work service")]
        public Uri ServiceUri { get; set; }

        [ArgRequired]
        [ArgShortcut("f")]
        [ArgDescription("The frequency to invoke the job at")]
        public JobRecurrenceFrequency Frequency { get; set; }

        [ArgRequired]
        [ArgShortcut("i")]
        [ArgDescription("The interval to invoke the job at (example: Frequency = Minute, Interval = 30 => Invoke every 30 minutes)")]
        public int Interval { get; set; }

        [ArgShortcut("ct")]
        [ArgDescription("The maximum number of invocations of this job. Defaults to infinite.")]
        public int? Count { get; set; }

        [ArgShortcut("et")]
        [ArgDescription("The time at which recurrence should cease. Defaults to never.")]
        public DateTime? EndTime { get; set; }

        [ArgShortcut("st")]
        [ArgDescription("The time at which the first invocation should occur. Defaults to now.")]
        public DateTime? StartTime { get; set; }

        protected override async Task OnExecute()
        {
            CloudService = String.IsNullOrEmpty(CloudService) ?
                String.Format("nuget-{0}-0-scheduler", TargetEnvironment.Name) :
                CloudService;

            if (!String.IsNullOrEmpty(EncodedPayload))
            {
                Payload = Encoding.UTF8.GetString(Convert.FromBase64String(EncodedPayload));
            }

            // Locate the work service 
            if (ServiceUri == null)
            {
                ServiceUri = TargetEnvironment.GetServiceUri("work");
            }

            if (ServiceUri == null)
            {
                await Console.WriteErrorLine(Strings.ParameterRequired, "SerivceUri");
            }
            else
            {
                Dictionary<string, string> payload = null;
                if(!String.IsNullOrEmpty(Payload)) {
                    payload = InvocationPayloadSerializer.Deserialize(Payload);
                }

                var bodyValue = new InvocationRequest(
                    Job, 
                    "Scheduler",
                    payload);
                var body = JsonConvert.SerializeObject(bodyValue);

                var request = new JobCreateOrUpdateParameters()
                {
                    StartTime = StartTime,
                    Action = new JobAction()
                    {
                        Type = JobActionType.Https,
                        Request = new JobHttpRequest()
                        {
                            Uri = new Uri(ServiceUri, "invocations"),
                            Method = "PUT",
                            Body = body,
                            Headers = new Dictionary<string, string>()
                            {
                                { "Content-Type", "application/json" },
                                { "Authorization", GenerateAuthHeader(Password) }
                            }
                        }
                        // TODO: Retry Policy
                    },
                    Recurrence = new JobRecurrence()
                    {
                        Count = Count,
                        EndTime = EndTime,
                        Frequency = Frequency,
                        Interval = Interval
                        // TODO: Schedule field?
                    }
                };
                
                using (var client = CloudContext.Clients.CreateSchedulerClient(Credentials, CloudService, Collection))
                {
                    await Console.WriteInfoLine(Strings.Scheduler_NewJobCommand_CreatingJob, Job, CloudService, Collection);
                    if (WhatIf)
                    {
                        await Console.WriteInfoLine(Strings.Scheduler_NewJobCommand_WouldCreateJob, JsonConvert.SerializeObject(request, new JsonSerializerSettings()
                        {
                            Formatting = Formatting.Indented,
                            ContractResolver = new CamelCasePropertyNamesContractResolver()
                        }));
                    }
                    else
                    {
                        var response = await client.Jobs.CreateOrUpdateAsync(InstanceName, request, CancellationToken.None);
                        await Console.WriteObject(response.Job);
                    }
                    await Console.WriteInfoLine(Strings.Scheduler_NewJobCommand_CreatedJob, Job, CloudService, Collection);
                }
            }
        }

        private string GenerateAuthHeader(string password)
        {
            return String.Concat("Basic ",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        String.Concat("admin:", password))));
        }
    }
}

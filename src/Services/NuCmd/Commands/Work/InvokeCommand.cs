using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Work;
using NuGet.Services.Work.Client;
using NuGet.Services.Work.Models;
using PowerArgs;

namespace NuCmd.Commands.Work
{
    [Description("Queues a command for immediate execution by the work service.")]
    public class InvokeCommand : WorkServiceCommandBase
    {
        [ArgRequired()]
        [ArgShortcut("j")]
        [ArgDescription("The job to invoke")]
        public string Job { get; set; }

        [ArgShortcut("s")]
        [ArgDescription("A value to report as the source of the job")]
        public string Source { get; set; }

        [ArgShortcut("p")]
        [ArgDescription("The JSON dictionary payload to provide to the job")]
        public string Payload { get; set; }

        [ArgShortcut("ep")]
        [ArgDescription("A base64-encoded UTF8 payload string to use. Designed for command-line piping")]
        public string EncodedPayload { get; set; }

        [ArgShortcut("i")]
        [ArgDescription("A unique name that will be used with UnlessAlreadyRunning to determine if an instance of this job is already running")]
        public string JobInstanceName { get; set; }

        [ArgShortcut("u")]
        [ArgDescription("Set this flag to queue this invocation only if the job is not already running with the same payload.")]
        public bool UnlessAlreadyRunning { get; set; }

        protected override async Task OnExecute()
        {
            if (!String.IsNullOrEmpty(EncodedPayload))
            {
                Payload = Encoding.UTF8.GetString(Convert.FromBase64String(EncodedPayload));
            }

            var client = await OpenClient();
            if (client == null) { return; }
            await Console.WriteTraceLine(Strings.Commands_UsingServiceUri, ServiceUri.AbsoluteUri);

            // Try to parse the payload
            Dictionary<string, string> payload = null;
            Exception thrown = null;
            try
            {
                payload = InvocationPayloadSerializer.Deserialize(Payload);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
            if (thrown != null)
            {
                await Console.WriteErrorLine(Strings.Work_InvokeCommand_PayloadInvalid, thrown.Message);
                return;
            }

            if (String.IsNullOrEmpty(Payload))
            {
                await Console.WriteInfoLine(Strings.Work_InvokeCommand_CreatingInvocation_NoPayload, Job);
            }
            else
            {
                await Console.WriteInfoLine(Strings.Work_InvokeCommand_CreatingInvocation_WithPayload, Job);
                await Console.WriteInfoLine(Payload);
            }
            if (!WhatIf)
            {
                var response = await client.Invocations.Put(new InvocationRequest(Job, Source)
                {
                    Payload = payload,
                    JobInstanceName = JobInstanceName,
                    UnlessAlreadyRunning = UnlessAlreadyRunning
                });

                if (await ReportHttpStatus(response))
                {
                    if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        await Console.WriteInfoLine(Strings.Work_InvokeCommand_AlreadyRunning);
                    }
                    else
                    {
                        var invocation = await response.ReadContent();
                        await Console.WriteInfoLine(Strings.Work_InvokeCommand_CreatedInvocation, invocation.Id.ToString("N").ToLowerInvariant());
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Work;
using NuGet.Services.Work.Client;
using NuGet.Services.Work.Models;
using PowerArgs;

namespace NuCmd.Commands.Work
{
    public class InvokeCommand : Command
    {
        [ArgRequired()]
        [ArgShortcut("u")]
        public Uri ServiceUri { get; set; }

        [ArgRequired()]
        [ArgShortcut("j")]
        public string Job { get; set; }

        [ArgShortcut("s")]
        public string Source { get; set; }

        [ArgShortcut("p")]
        public string Payload { get; set; }

        protected override async Task OnExecute()
        {
            var client = new WorkClient(ServiceUri);
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

            await Console.WriteInfoLine(Strings.Work_InvokeCommand_CreatingInvocation, Job, Payload);
            if (!WhatIf)
            {
                var response = await client.Invocations.Put(new InvocationRequest(Job, Source)
                {
                    Payload = payload
                });

                if (!response.IsSuccessStatusCode)
                {
                    await Console.WriteErrorLine(
                        Strings.Commands_HttpError,
                        response.StatusCode,
                        response.ReasonPhrase);
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

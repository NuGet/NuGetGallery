using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Work;
using PowerArgs;

namespace NuCmd.Commands.Work
{
    [Description("Invokes a worker job immediately, locally, and with the local console as a trace sink")]
    public class RunCommand : Command
    {
        [ArgRequired()]
        [ArgShortcut("j")]
        [ArgDescription("The job to invoke")]
        public string Job { get; set; }

        [ArgShortcut("p")]
        [ArgDescription("The JSON dictionary payload to provide to the job")]
        public string Payload { get; set; }

        [ArgShortcut("ep")]
        [ArgDescription("A base64-encoded UTF8 payload string to use. Designed for command-line piping")]
        public string EncodedPayload { get; set; }

        protected override async Task OnExecute()
        {
            if (!String.IsNullOrEmpty(EncodedPayload))
            {
                Payload = Encoding.UTF8.GetString(Convert.FromBase64String(EncodedPayload));
            }

            var service = await LocalWorker.Create();
            service.RunJob(Job, Payload);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Newtonsoft.Json;
using NuGet.Services.Models;
using PowerArgs;

namespace NuCmd.Commands.Work
{
    public class LogCommand : WorkServiceCommandBase
    {
        [ArgRequired()]
        [ArgShortcut("i")]
        [ArgPosition(0)]
        [ArgDescription("The ID of the invocation to get the log for")]
        public string Id { get; set; }

        protected override async Task OnExecute()
        {
            var client = await OpenClient();
            if (client == null) { return; }

            await Console.WriteInfoLine(Strings.Work_LogCommand_FetchingLog, Id);
            var response = await client.Invocations.GetLog(Id);
            if (await ReportHttpStatus(response))
            {
                var log = await response.ReadContent();
                var events = LogEvent.ParseLogEvents(log);
                string message = String.Format(Strings.Work_LogCommand_RenderingLog, Id);
                await Console.WriteInfoLine(message);
                await Console.WriteInfoLine(new String('-', message.Length));
                foreach (var evt in events)
                {
                    await WriteEvent(evt);
                }
                message = String.Format(Strings.Work_LogCommand_RenderedLog, Id);
                await Console.WriteInfoLine(new String('-', message.Length));
                await Console.WriteInfoLine(message);
            }
        }

        private async Task WriteEvent(LogEvent evt)
        {
            string message = evt.Message;
            switch (evt.Level)
            {
                case LogEventLevel.Critical:
                    await Console.WriteFatalLine(message);
                    break;
                case LogEventLevel.Error:
                    await Console.WriteErrorLine(message);
                    break;
                case LogEventLevel.Informational:
                    await Console.WriteInfoLine(message);
                    break;
                case LogEventLevel.Verbose:
                    await Console.WriteTraceLine(message);
                    break;
                case LogEventLevel.Warning:
                    await Console.WriteWarningLine(message);
                    break;
            }
        }
    }
}

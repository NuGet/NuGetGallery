using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Reactive.Linq;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using NuGet.Services.Work;
using PowerArgs;
using System.Reactive;

namespace NuCmd.Commands.Work
{
    [Description("Invokes a worker job immediately, locally, and with the local console as a trace sink")]
    public class RunCommand : Command
    {
        [ArgRequired()]
        [ArgPosition(0)]
        [ArgShortcut("j")]
        [ArgDescription("The job to invoke")]
        public string Job { get; set; }

        [ArgShortcut("p")]
        [ArgDescription("The JSON dictionary payload to provide to the job")]
        public string Payload { get; set; }

        [ArgShortcut("ep")]
        [ArgDescription("A base64-encoded UTF8 payload string to use. Designed for command-line piping")]
        public string EncodedPayload { get; set; }

        [ArgShortcut("c")]
        [ArgDescription("The JSON dictionary configuration to provide to the job")]
        public string Configuration { get; set; }

        [ArgShortcut("ec")]
        [ArgDescription("A base64-encoded UTF8 configuration string to use. Designed for command-line piping")]
        public string EncodedConfiguration { get; set; }

        protected override async Task OnExecute()
        {
            if (!String.IsNullOrEmpty(EncodedPayload))
            {
                Payload = Encoding.UTF8.GetString(Convert.FromBase64String(EncodedPayload));
            }

            if (!String.IsNullOrEmpty(EncodedConfiguration))
            {
                Configuration = Encoding.UTF8.GetString(Convert.FromBase64String(EncodedConfiguration));
            }

            var configuration = InvocationPayloadSerializer.Deserialize(Configuration);

            var service = await LocalWorkService.Create(configuration);

            var tcs = new TaskCompletionSource<object>();

            string message = String.Format(Strings.Work_RunCommand_Invoking, Job);
            await Console.WriteInfoLine(message);
            await Console.WriteInfoLine(new String('-', message.Length));

            Exception thrown = null;
            try
            {
                var observable = service.RunJob(Job, Payload);
                observable
                    .Subscribe(
                        evt => RenderEvent(evt).Wait(),
                        ex => tcs.SetException(ex),
                        () => tcs.SetResult(null));
                await tcs.Task;
            }
            catch (AggregateException aex)
            {
                thrown = aex.InnerException;
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
            if (thrown != null)
            {
                await Console.WriteErrorLine(thrown.ToString());
            }

            message = String.Format(Strings.Work_RunCommand_Invoked, Job);
            await Console.WriteInfoLine(new String('-', message.Length));
            await Console.WriteInfoLine(message);
        }

        private async Task RenderEvent(EventEntry evt)
        {
            string message = evt.FormattedMessage;
            switch (evt.Schema.Level)
            {
                case EventLevel.Critical:
                    await Console.WriteFatalLine(message);
                    break;
                case EventLevel.Error:
                    await Console.WriteErrorLine(message);
                    break;
                case EventLevel.Informational:
                    await Console.WriteInfoLine(message);
                    break;
                case EventLevel.Verbose:
                    await Console.WriteTraceLine(message);
                    break;
                case EventLevel.Warning:
                    await Console.WriteWarningLine(message);
                    break;
            }
        }
    }
}

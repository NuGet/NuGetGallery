using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Work.Models;
using PowerArgs;

namespace NuCmd.Commands.Work
{
    [Description("Gets invocation statistics from the service")]
    public class StatsCommand : WorkServiceCommandBase
    {
        public enum StatsType
        {
            Summary,
            ByJob,
            ByInstance
        }

        [ArgShortcut("-t")]
        [ArgPosition(0)]
        [ArgDescription("Specifies the type of statistics to retrieve")]
        [PowerArgs.DefaultValue("Summary")]
        public StatsType Type { get; set; }

        protected override Task OnExecute()
        {
            switch (Type)
            {
                case StatsType.Summary:
                    return StatsSummary();
                case StatsType.ByJob:
                    return StatsByJob();
                case StatsType.ByInstance:
                    return StatsByInstance();
                default:
                    return Console.WriteErrorLine(Strings.Work_StatsCommand_UnknownStatsType, Type.ToString());
            }
        }

        private async Task StatsByInstance()
        {
            var client = await OpenClient();
            if (client == null) { return; }

            var response = await client.Workers.GetStatistics();

            if (await ReportHttpStatus(response))
            {
                var statistics = await response.ReadContent();
                await Console.WriteTable(statistics, s => new
                {
                    s.Instance,
                    s.Queued,
                    s.Executing,
                    s.Suspended,
                    s.Executed,
                    s.Completed,
                    s.Faulted
                });
            }
        }

        private async Task StatsByJob()
        {
            var client = await OpenClient();
            if (client == null) { return; }

            var response = await client.Jobs.GetStatistics();

            if (await ReportHttpStatus(response))
            {
                var statistics = await response.ReadContent();
                await Console.WriteTable(statistics, s => new
                {
                    s.Job,
                    s.Queued,
                    s.Executing,
                    s.Suspended,
                    s.Executed,
                    s.Completed,
                    s.Faulted
                });
            }
        }

        private async Task StatsSummary()
        {
            var client = await OpenClient();
            if (client == null) { return; }

            var response = await client.Invocations.GetStatistics();

            if (await ReportHttpStatus(response))
            {
                await Console.WriteObject(await response.ReadContent());
            }
        }
    }
}

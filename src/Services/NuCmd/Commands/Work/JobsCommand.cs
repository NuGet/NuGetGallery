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
    [Description("Lists available jobs")]
    public class JobsCommand : WorkServiceCommandBase
    {
        protected override async Task OnExecute()
        {
            var client = await OpenClient();
            if (client == null) { return; }

            var response = await client.Jobs.Get();

            if (await ReportHttpStatus(response))
            {
                var jobs = await response.ReadContent();
                await Console.WriteTable(
                    jobs,
                    j => j.Name,
                    j => j.Description,
                    j => j.Enabled,
                    j => new { Assembly = j.Assembly.FullName.Name }.Assembly, // Hack to get the name to show up right
                    j => j.Assembly.BuildCommit,
                    j => j.Assembly.BuildDate);
            }
        }
    }
}

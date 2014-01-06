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
            var client = OpenClient();

            var response = await client.Jobs.Get();

            if (await ReportHttpStatus(response))
            {
                var jobs = await response.ReadContent();
                await Console.WriteTable(
                    jobs,
                    j => j.Name,
                    j => j.Description,
                    j => j.Enabled,
                    j => j.Assembly.BuildCommit,
                    j => j.Assembly.BuildDate);
            }
        }
    }
}

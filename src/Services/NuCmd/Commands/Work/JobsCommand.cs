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
                    jobs, j => new
                    {
                        j.Name,
                        j.Description,
                        j.Enabled,
                        Assembly = j.Assembly.FullName.Name,
                        j.Assembly.BuildCommit,
                        j.Assembly.BuildDate
                    });
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Work;
using NuGet.Services.Work.Models;
using PowerArgs;

namespace NuCmd.Commands.Work
{
    [Description("Lists available jobs")]
    public class JobsCommand : WorkServiceCommandBase
    {
        [ArgShortcut("l")]
        [ArgDescription("Set this flag to view only jobs available when using 'nucmd work run'")]
        public bool Local { get; set; }

        protected override async Task OnExecute()
        {
            if (Local)
            {
                await Console.WriteTable(
                    WorkService.GetAllAvailableJobs(), j => new
                    {
                        j.Name,
                        j.Description,
                        j.Enabled,
                        Assembly = j.Assembly.FullName.Name,
                        j.Assembly.BuildCommit,
                        j.Assembly.BuildDate
                    });
            }
            else
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
}

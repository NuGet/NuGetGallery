using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Scheduler;
using Microsoft.WindowsAzure.Scheduler.Models;
using PowerArgs;

namespace NuCmd.Commands.Scheduler
{
    [Description("Lists the jobs in the specified collection")]
    public class JobsCommand : SchedulerCommandBase
    {
        [ArgShortcut("cs")]
        [ArgDescription("Specifies the scheduler service for the collection. Defaults to the standard one for this environment (nuget-[environment]-0-scheduler)")]
        public string CloudService { get; set; }

        [ArgShortcut("c")]
        [ArgDescription("The job collection to list jobs from")]
        public string Collection { get; set; }

        [ArgShortcut("j")]
        [ArgPosition(0)]
        [ArgDescription("Specify this value to retrieve information on a specific job.")]
        public string Id { get; set; }

        protected override async Task OnExecute()
        {
            CloudService = String.IsNullOrEmpty(CloudService) ?
                String.Format("nuget-{0}-0-scheduler", TargetEnvironment.Name) :
                CloudService;
            Collection = String.IsNullOrEmpty(Collection) ?
                String.Format("nuget-{0}-0-scheduler-0", TargetEnvironment.Name) :
                Collection;

            using (var client = CloudContext.Clients.CreateSchedulerClient(Credentials, CloudService, Collection))
            {
                await Console.WriteInfoLine(Strings.Scheduler_JobsCommand_ListingJobs, CloudService, Collection);
                if (String.IsNullOrEmpty(Id))
                {
                    var jobs = await client.Jobs.ListAsync(new JobListParameters(), CancellationToken.None);
                    await Console.WriteTable(jobs, r => new
                        {
                            r.Id,
                            r.State,
                            r.Status
                        });
                }
                else
                {
                    var job = await client.Jobs.GetAsync(Id, CancellationToken.None);
                    await Console.WriteObject(job.Job);
                }
            }
        }
    }
}

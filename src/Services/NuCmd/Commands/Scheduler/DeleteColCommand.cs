using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Scheduler;
using PowerArgs;

namespace NuCmd.Commands.Scheduler
{
    [Description("Deletes the specified scheduler job collection")]
    public class DeleteColCommand : SchedulerCommandBase
    {
        [ArgShortcut("cs")]
        [ArgDescription("Specifies the scheduler service for the collection. Defaults to the standard one for this environment (nuget-[environment]-0-scheduler)")]
        public string CloudService { get; set; }

        [ArgRequired]
        [ArgShortcut("c")]
        [ArgPosition(0)]
        [ArgDescription("The name of the collection to delete")]
        public string Name { get; set; }

        protected override async Task OnExecute()
        {
            CloudService = String.IsNullOrEmpty(CloudService) ?
                String.Format("nuget-{0}-0-scheduler", TargetEnvironment.Name) :
                CloudService;

            using (var client = CloudContext.Clients.CreateSchedulerManagementClient(Credentials))
            {
                await Console.WriteInfoLine(Strings.Scheduler_ColDeleteCommand_DeletingCollection, CloudService, Name);
                await client.JobCollections.DeleteAsync(CloudService, Name);
            }
        }
    }
}

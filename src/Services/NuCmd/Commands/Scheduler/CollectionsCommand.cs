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
    [Description("Lists the scheduler job collections available")]
    public class CollectionsCommand : SchedulerCommandBase
    {
        [ArgShortcut("cs")]
        [ArgDescription("Specifies the scheduler service for the collection. Defaults to the standard one for this environment (nuget-[environment]-0-scheduler)")]
        public string CloudService { get; set; }

        [ArgShortcut("c")]
        [ArgPosition(0)]
        [ArgDescription("The name of a specific job collection to view information about")]
        public string Name { get; set; }

        protected override Task OnExecute()
        {
            CloudService = String.IsNullOrEmpty(CloudService) ?
                String.Format("nuget-{0}-0-scheduler", TargetEnvironment.Name) :
                CloudService;

            if (String.IsNullOrEmpty(Name))
            {
                return GetAllCollections();
            }
            else
            {
                return GetSingleCollection();
            }
        }

        private async Task GetSingleCollection()
        {
            using (var client = CloudContext.Clients.CreateSchedulerManagementClient(Credentials))
            {
                await Console.WriteInfoLine(Strings.Scheduler_CollectionsCommand_GettingCollection, Name, CloudService);
                var response = await client.JobCollections.GetAsync(CloudService, Name);
                await Console.WriteObject(response);
            }
        }

        private async Task GetAllCollections()
        {
            using (var client = CloudContext.Clients.CreateCloudServiceManagementClient(Credentials))
            {
                await Console.WriteInfoLine(Strings.Scheduler_CollectionsCommand_ListingCollections, CloudService);
                var response = await client.CloudServices.GetAsync(CloudService);
                await Console.WriteTable(response.Resources.Where(r =>
                    String.Equals(r.ResourceProviderNamespace, "scheduler", StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(r.Type, "jobcollections", StringComparison.OrdinalIgnoreCase)),
                    r => new
                    {
                        r.Name,
                        r.State,
                        r.SubState,
                        r.Plan,
                        r.OutputItems
                    });
            }
        }
    }
}

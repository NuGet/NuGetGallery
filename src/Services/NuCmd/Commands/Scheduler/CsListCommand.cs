using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Scheduler;

namespace NuCmd.Commands.Scheduler
{
    [Description("Lists the scheduler services available")]
    public class CsListCommand : SchedulerCommandBase
    {
        protected override async Task OnExecute()
        {
            using (var client = CloudContext.Clients.CreateCloudServiceManagementClient(Credentials))
            {
                await Console.WriteInfoLine(Strings.Scheduler_CsListCommand_ListingAvailableServices);
                var response = await client.CloudServices.ListAsync();
                await Console.WriteTable(response,
                    r => r.Name,
                    r => r.Label,
                    r => r.Description,
                    r => r.GeoRegion);
            }
        }
    }
}

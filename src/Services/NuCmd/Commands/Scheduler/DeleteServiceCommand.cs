using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Scheduler;
using PowerArgs;

namespace NuCmd.Commands.Scheduler
{
    [Description("Deletes the specified scheduler service")]
    public class DeleteServiceCommand : SchedulerCommandBase
    {
        [ArgRequired]
        [ArgPosition(0)]
        [ArgShortcut("cs")]
        public string Name { get; set; }

        protected override async Task OnExecute()
        {
            using (var client = CloudContext.Clients.CreateCloudServiceManagementClient(Credentials))
            {
                await Console.WriteInfoLine(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Scheduler_CsDeleteCommand_DeletingService,
                    Name));
                if (!WhatIf)
                {
                    await client.CloudServices.DeleteAsync(Name);
                }
                await Console.WriteInfoLine(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Scheduler_CsDeleteCommand_DeletedService,
                    Name));
            }
        }
    }
}

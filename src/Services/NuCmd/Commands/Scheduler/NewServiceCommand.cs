using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Scheduler;
using Microsoft.WindowsAzure.Management.Scheduler.Models;
using PowerArgs;

namespace NuCmd.Commands.Scheduler
{
    [Description("Creates a scheduler service")]
    public class NewServiceCommand : SchedulerCommandBase
    {
        [ArgRequired]
        [ArgShortcut("cs")]
        [ArgPosition(0)]
        [ArgDescription("The name of the service to create")]
        public string Name { get; set; }

        [ArgShortcut("d")]
        [ArgDescription("A description of the service")]
        public string Description { get; set; }

        [ArgShortcut("e")]
        [ArgDescription("An email address for the owner of the service")]
        public string Email { get; set; }

        [ArgShortcut("r")]
        [ArgDescription("The geographic region in which to place the service")]
        public string GeoRegion { get; set; }

        [ArgShortcut("l")]
        [ArgDescription("A label for the service")]
        public string Label { get; set; }

        protected override async Task OnExecute()
        {
            using (var client = CloudContext.Clients.CreateCloudServiceManagementClient(Credentials))
            {
                await Console.WriteInfoLine(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Scheduler_CsNewCommand_CreatingService,
                    Name));
                if (!WhatIf)
                {
                    await client.CloudServices.CreateAsync(
                        Name,
                        new CloudServiceCreateParameters()
                        {
                            Description = Description,
                            Email = Email,
                            GeoRegion = GeoRegion,
                            Label = Label
                        });
                }
                await Console.WriteInfoLine(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Scheduler_CsNewCommand_CreatedService,
                    Name));
            }
        }
    }
}

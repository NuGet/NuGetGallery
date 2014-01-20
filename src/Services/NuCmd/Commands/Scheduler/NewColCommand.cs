using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Scheduler.Models;
using PowerArgs;
using DefaultValueAttribute = PowerArgs.DefaultValueAttribute;

namespace NuCmd.Commands.Scheduler
{
    [Description("Creates a scheduler job collection")]
    public class NewColCommand : SchedulerCommandBase
    {
        [ArgShortcut("cs")]
        [ArgDescription("Specifies the scheduler service for the collection. Defaults to the standard one for this environment (nuget-[environment]-0-scheduler)")]
        public string CloudService { get; set; }

        [ArgRequired]
        [ArgPosition(0)]
        [ArgDescription("The name of the collection")]
        public string Name { get; set; }

        [ArgShortcut("p")]
        [DefaultValue("Free")]
        [ArgDescription("The scheduler pricing plan to use for the collection. See http://www.windowsazure.com/en-us/pricing/details/scheduler/ for pricing details.")]
        public JobCollectionPlan Plan { get; set; }

        [ArgShortcut("l")]
        [ArgDescription("A friendly label to apply to the collection")]
        public string Label { get; set; }

        [ArgShortcut("mo")]
        [ArgDescription("Maximum number of occurrences for a job?")]
        public int? MaxJobOccurrence { get; set; }

        [ArgShortcut("mj")]
        [ArgDescription("Maximum number of jobs")]
        public int? MaxJobCount { get; set; }
        
        [ArgShortcut("mrf")]
        [ArgDescription("Maximum recurrence frequency")]
        public JobCollectionRecurrenceFrequency? MaxRecurrenceFrequency { get; set; }

        [ArgShortcut("mri")]
        [ArgDescription("Maximum recurrence interval")]
        public int? MaxRecurrenceInterval { get; set; }

        protected override async Task OnExecute()
        {
            if((MaxRecurrenceFrequency.HasValue && !MaxRecurrenceInterval.HasValue) ||
                (MaxRecurrenceInterval.HasValue && !MaxRecurrenceFrequency.HasValue)) {
                await Console.WriteErrorLine(Strings.Scheduler_ColNewCommand_MaxRecurrenceIncomplete);
            }
            else {
                CloudService = String.IsNullOrEmpty(CloudService) ?
                    String.Format("nuget-{0}-0-scheduler", TargetEnvironment.Name) :
                    CloudService;

                JobCollectionMaxRecurrence maxRecurrence = null;
                if(MaxRecurrenceFrequency != null) {
                    maxRecurrence = new JobCollectionMaxRecurrence()
                    {
                        Frequency = MaxRecurrenceFrequency.Value,
                        Interval = MaxRecurrenceInterval.Value
                    };
                }

                using (var client = CloudContext.Clients.CreateSchedulerManagementClient(Credentials))
                {
                    await Console.WriteInfoLine(Strings.Scheduler_ColNewCommand_CreatingCollection, Name, CloudService);
                    if (!WhatIf)
                    {
                        await client.JobCollections.CreateAsync(
                            CloudService,
                            Name,
                            new JobCollectionCreateParameters()
                            {
                                Label = Label,
                                IntrinsicSettings = new JobCollectionIntrinsicSettings()
                                {
                                    Plan = Plan,
                                    Quota = new JobCollectionQuota()
                                    {
                                        MaxJobCount = MaxJobCount,
                                        MaxJobOccurrence = MaxJobOccurrence,
                                        MaxRecurrence = maxRecurrence
                                    }
                                }
                            },
                            CancellationToken.None);
                    }
                    await Console.WriteInfoLine(Strings.Scheduler_ColNewCommand_CreatedCollection, Name, CloudService);
                }
            }
        }
    }
}

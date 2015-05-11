// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Management.Scheduler.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks.Subscription
{
    [Command("createjobcollection", "Creates a new Scheduler Job Collection in the Subscription", AltName = "cjc", MinArgs = 1, MaxArgs = 1)]
    public class CreateJobCollectionTask : SubscriptionTask
    {
        [Option("The cloud service in which to create the collection", AltName = "cs")]
        public string CloudService { get; set; }

        [Option("A label to give the job collection", AltName = "l")]
        public string Label { get; set; }

        [Option("The plan for the collection", AltName = "p")]
        public JobCollectionPlan Plan { get; set; }

        [Option("The maximum number of jobs in the collection", AltName = "maxj")]
        public int? MaxJobCount { get; set; }

        [Option("The maximum job occurrence in the collection", AltName = "maxo")]
        public int? MaxJobOccurrence { get; set; }

        [Option("The maximum recurrence frequency", AltName = "maxrf")]
        public JobCollectionRecurrenceFrequency? MaxRecurrenceFrequency { get; set; }

        [Option("The maximum recurrence interval. Required if MaxRecurrenceFrequency is specified", AltName = "maxri")]
        public int? MaxRecurrenceInterval { get; set; }

        public CreateJobCollectionTask()
        {
            Plan = JobCollectionPlan.Free;
        }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            ArgCheck.Required(Label, "Label");
            ArgCheck.Required(CloudService, "CloudService");

            if (MaxRecurrenceFrequency.HasValue)
            {
                ArgCheck.Required(MaxRecurrenceInterval, "MaxRecurrenceInterval");
            }
        }

        public override void ExecuteCommand()
        {
            var cred = new CertificateCloudCredentials(SubscriptionId, ManagementCertificate);
            var schman = CloudContext.Clients.CreateSchedulerManagementClient(cred);

            var createParams = new JobCollectionCreateParameters()
            {
                Label = Label,
                IntrinsicSettings = new JobCollectionIntrinsicSettings()
                {
                    Plan = Plan,
                    Quota = new JobCollectionQuota()
                    {
                        MaxJobCount = MaxJobCount,
                        MaxJobOccurrence = MaxJobOccurrence,
                        MaxRecurrence = MaxRecurrenceFrequency.HasValue ? new JobCollectionMaxRecurrence()
                        {
                            Frequency = MaxRecurrenceFrequency.Value,
                            Interval = MaxRecurrenceInterval.Value
                        } : null
                    }
                }
            };

            if (WhatIf)
            {
                Log.Info("Would create job collection {0} in {1} with the following params:", Arguments[0], CloudService);
                Log.Info(JsonConvert.SerializeObject(createParams, new StringEnumConverter()));
            }
            else
            {
                Log.Info("Creating job collection {0} in {1}...", Arguments[0], CloudService);
                schman.JobCollections.CreateAsync(
                    CloudService,
                    Arguments[0],
                    createParams,
                    CancellationToken.None).Wait();
            }
        }
    }
}

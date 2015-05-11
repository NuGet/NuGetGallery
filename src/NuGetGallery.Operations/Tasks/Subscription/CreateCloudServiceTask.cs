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
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks.Subscription
{
    [Command("createcloudservice", "Creates a new Cloud Service in the Subscription", AltName = "ccs", MinArgs = 1, MaxArgs = 1)]
    public class CreateCloudServiceTask : SubscriptionTask
    {
        [Option("A description of the cloud service", AltName="d")]
        public string Description { get; set; }

        [Option("An email address to attach to the cloud service", AltName = "e")]
        public string Email { get; set; }

        [Option("The region in which to place the cloud service", AltName = "r")]
        public string Region { get; set; }

        [Option("A label for the cloud service", AltName = "l")]
        public string Label { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(Description, "Description");
            ArgCheck.Required(Email, "Email");
            ArgCheck.Required(Region, "Region");
            ArgCheck.Required(Label, "Label");
        }

        public override void ExecuteCommand()
        {
            var cred = new CertificateCloudCredentials(SubscriptionId, ManagementCertificate);
            var csman = CloudContext.Clients.CreateCloudServiceManagementClient(cred);
            var result = csman.CloudServices.CreateAsync(
                Arguments[0], new CloudServiceCreateParameters()
                {
                    Label = Label,
                    GeoRegion = Region,
                    Email = Email,
                    Description = Description
                }, CancellationToken.None).Result;
            Log.Info("Created Cloud Service {0}", Arguments[0]);
        }
    }
}

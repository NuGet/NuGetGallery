// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;

namespace NuGetGallery.Operations.Tasks.Subscription
{
    [Command("listcloudservices", "Lists available Cloud Services in the Subscription", AltName="lcs")]
    public class ListCloudServicesTask : SubscriptionTask
    {
        public override void ExecuteCommand()
        {
            var cred = new CertificateCloudCredentials(SubscriptionId, ManagementCertificate);
            var csman = CloudContext.Clients.CreateCloudServiceManagementClient(cred);
            var svcs = csman.CloudServices.ListAsync(CancellationToken.None).Result;
            foreach (var svc in svcs)
            {
                Log.Info("* {0} ({1}): {2}", svc.Name, svc.GeoRegion, svc.Description);
            }
        }
    }
}

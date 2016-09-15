// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.ServiceRuntime;
using NuGetGallery.Auditing;
using System;

namespace NuGetGallery.Configuration.Factory
{
    public class CloudAuditingServiceFactory : ConfigObjectFactory<CloudAuditingService>
    {
        private static CloudAuditingService CloudAuditingServiceConstructorWrapper(string connectionString)
        {
            string instanceId;
            try
            {
                instanceId = RoleEnvironment.CurrentRoleInstance.Id;
            }
            catch
            {
                instanceId = Environment.MachineName;
            }

            var localIp = AuditActor.GetLocalIP().Result;

            return new CloudAuditingService(instanceId, localIp, connectionString, CloudAuditingService.GetAspNetOnBehalfOf);
        }

        public CloudAuditingServiceFactory()
            : base(new ConfigObjectDelegate<CloudAuditingService>(
                objects => CloudAuditingServiceConstructorWrapper((string)objects[0]), "AzureStorageConnectionString"))
        {
        }
    }
}
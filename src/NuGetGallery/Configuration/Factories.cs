using Elmah;
using Microsoft.WindowsAzure.ServiceRuntime;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Auditing;
using NuGetGallery.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Configuration
{
    public static class Factories
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

        public static ConfigObjectFactory<CloudAuditingService> AuditingService => new ConfigObjectFactory<CloudAuditingService>(
                objects => CloudAuditingServiceConstructorWrapper((string)objects[0]), "AzureStorageConnectionString");

        public static ConfigObjectFactory<EntitiesContext> EntitiesContext => new ConfigObjectFactory<EntitiesContext>(
            objects => new EntitiesContext((string)objects[0], (bool)objects[1]), new string[] { "SqlConnectionString", "ReadOnlyMode" });

        public static ConfigObjectFactory<SqlErrorLog> SqlErrorLog => new ConfigObjectFactory<SqlErrorLog>(
            objects => new SqlErrorLog((string)objects[0]), "SqlConnectionString");

        public static ConfigObjectFactory<TableErrorLog> TableErrorLog => new ConfigObjectFactory<TableErrorLog>(
            objects => new TableErrorLog((string)objects[0]), "AzureStorageConnectionString");

        public static ConfigObjectFactory<SupportRequestDbContext> SupportRequestDbContext => new ConfigObjectFactory<SupportRequestDbContext>(
            objects => new SupportRequestDbContext((string)objects[0]), "SqlConnectionStringSupportRequest");
    }
}
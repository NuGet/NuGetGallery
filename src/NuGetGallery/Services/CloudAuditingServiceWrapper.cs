using System;
using System.Threading.Tasks;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGetGallery.Services
{
    public class CloudAuditingServiceWrapper : CloudAuditingService
    {
        private IGalleryConfigurationService _configService;

        private string _connectionString;

        public CloudAuditingServiceWrapper(IGalleryConfigurationService configService)
            : base(GetInstanceId(), GetLocalIp(), CloudAuditingService.GetContainer(configService.Current.AzureStorageConnectionString), CloudAuditingService.GetAspNetOnBehalfOf)
        {
            _configService = configService;
            _connectionString = configService.Current.AzureStorageConnectionString;
        }

        private static string GetInstanceId()
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

            return instanceId;
        }

        private static string GetLocalIp()
        {
            return AuditActor.GetLocalIP().Result;
        }

        protected override Task<Uri> SaveAuditRecord(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
        {
            var oldConnectionString = _connectionString;
            _connectionString = _configService.Current.AzureStorageConnectionString;

            if (oldConnectionString != _connectionString)
            {
                _auditContainer = GetContainer(_connectionString);
            }

            return base.SaveAuditRecord(auditData, resourceType, filePath, action, timestamp);
        }
    }
}
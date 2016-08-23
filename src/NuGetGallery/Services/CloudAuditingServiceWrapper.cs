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

        public async Task<CloudAuditingServiceWrapper> CreateCloudAuditingService(IGalleryConfigurationService configService)
        {
            return new CloudAuditingServiceWrapper(configService, (await _configService.GetCurrent()).AzureStorageConnectionString);
        }

        private CloudAuditingServiceWrapper(IGalleryConfigurationService configService, string connectionString)
            : base(GetInstanceId(), GetLocalIp(), CloudAuditingService.GetContainer(connectionString), CloudAuditingService.GetAspNetOnBehalfOf)
        {
            _configService = configService;
            _connectionString = connectionString;
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

        protected override async Task<Uri> SaveAuditRecord(string auditData, string resourceType, string filePath, string action, DateTime timestamp)
        {
            var oldConnectionString = _connectionString;
            _connectionString = (await _configService.GetCurrent()).AzureStorageConnectionString;

            if (oldConnectionString != _connectionString)
            {
                _auditContainer = GetContainer(_connectionString);
            }

            return await base.SaveAuditRecord(auditData, resourceType, filePath, action, timestamp);
        }
    }
}
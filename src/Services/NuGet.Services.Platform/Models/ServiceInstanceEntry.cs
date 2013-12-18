using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;

namespace NuGet.Services.Models
{
    [Table("Services")]
    public class ServiceInstanceEntry : AzureTableEntity
    {
        private ServiceHostName _key = null;

        [IgnoreProperty]
        public ServiceHostName Host
        {
            get { return _key ?? (_key = ParseKey()); }
            set { _key = value; KeyChanged(); }
        }

        public override string PartitionKey
        {
            get { return base.PartitionKey; }
            set { base.PartitionKey = value; _key = ParseKey(); }
        }

        public string Environment { get; set; }
        public int DatacenterId { get; set; }
        public string ServiceHost { get; set; }
        public string FullName { get; set; }
        public string Name { get; set; }
        public int Instance { get; set; }

        public string BuildCommit { get; set; }
        public string BuildBranch { get; set; }
        public DateTimeOffset BuildDate { get; set; }
        public Uri SourceCodeRepository { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? LastHeartbeat { get; set; }

        [Obsolete("For serialization only")]
        public ServiceInstanceEntry() { }

        public ServiceInstanceEntry(ServiceInstanceName instance, AssemblyInformation runtime)
        {
            Host = instance.Host;
            RowKey = instance.ShortName;
            Timestamp = DateTimeOffset.UtcNow;

            Environment = instance.Host.Datacenter.Environment;
            DatacenterId = instance.Host.Datacenter.Id;
            ServiceHost = instance.Host.Name;
            FullName = instance.ShortName;
            Name = instance.ServiceName;
            Instance = instance.InstanceId;

            BuildCommit = runtime.BuildCommit;
            BuildBranch = runtime.BuildBranch;
            BuildDate = runtime.BuildDate;
            SourceCodeRepository = runtime.SourceCodeRepository;
        }

        public static TableStorageKey GetKey(ServiceInstanceName serviceInstanceName)
        {
            return new TableStorageKey(
                serviceInstanceName.Host.ToString().ToLowerInvariant(),
                serviceInstanceName.ShortName);
        }

        private ServiceHostName ParseKey()
        {
            return ServiceHostName.Parse(PartitionKey);
        }

        private void KeyChanged()
        {
            PartitionKey = Host.ToString().ToLowerInvariant();
        }

        internal static ServiceInstanceEntry FromService(NuGetService service)
        {
            return new ServiceInstanceEntry(
                service.InstanceName,
                AssemblyInformation.FromObject(service));
        }
    }
}

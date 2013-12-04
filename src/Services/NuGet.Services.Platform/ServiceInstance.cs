using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;
using NuGetGallery.Storage;

namespace NuGet.Services
{
    [Table("ServiceInstances")]
    public class ServiceInstance : AzureTableEntity
    {
        [IgnoreProperty]
        public string Name { get { return RowKey; } set { RowKey = value; } }

        [IgnoreProperty]
        public string Service { get { return PartitionKey; } set { PartitionKey = value; } }

        public string MachineName { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset LastHeartbeat { get; set; }

        public ServiceInstance(string service, string name, string machineName, DateTimeOffset startedAt, DateTimeOffset lastHeartbeat)
            : base(service, name, lastHeartbeat)
        {
            MachineName = machineName;
            StartedAt = startedAt;
            LastHeartbeat = lastHeartbeat;
        }
    }
}

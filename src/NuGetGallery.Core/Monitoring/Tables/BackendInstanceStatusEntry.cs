using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGetGallery.Monitoring.Tables
{
    public class BackendInstanceStatusEntry : TableEntity, IMonitoringTableEntry
    {
        public string ServiceName { get { return PartitionKey; } }
        public string InstanceName { get { return RowKey; } }

        public BackendInstanceStatusEntry() { }
        public BackendInstanceStatusEntry(string serviceName, string instanceName)
        {
            PartitionKey = serviceName;
            InstanceName = instanceName;
        }

        public BackendInstanceStatus Status { get; set; }
        public Guid LastInvocation { get; set; }
        public string LastJob { get; set; }
        public string LastMessage { get; set; }
        public DateTimeOffset LastUpdatedAt { get; set; }
    }

    public enum BackendInstanceStatus
    {
        Started,
        Idle,
        Executing
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using NuGetGallery.Jobs;

namespace NuGetGallery.Monitoring.Tables
{
    public class BackendJobInvocationEntry : TableEntity, IMonitoringTableEntry
    {
        public string JobName { get { return PartitionKey; } }
        public string InvocationId { get { return RowKey; } }

        public BackendJobInvocationEntry() { }
        public BackendJobInvocationEntry(string jobName, Guid invocationId)
        {
            PartitionKey = serviceName;
            InvocationId = invocationId.ToString("N");
        }

        public JobStatus Status { get; set; }
        public DateTimeOffset RecievedAt { get; set; }
        public string Source { get; set; }
        public string Payload { get; set; }
        public string LogUrl { get; set; }
    }
}

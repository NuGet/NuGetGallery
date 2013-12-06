using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;
using NuGetGallery;
using NuGetGallery.Storage;

namespace NuGet.Services
{
    [Table("ServiceInstances")]
    public class ServiceInstance : AzureTableEntity
    {
        [IgnoreProperty]
        public string Name { get { return RowKey; } set { RowKey = value; } }

        [IgnoreProperty]
        public string Host { get { return PartitionKey; } set { PartitionKey = value; } }

        public string Service { get; set; }
        public string BuildCommit { get; set; }
        public string BuildBranch { get; set; }
        public DateTimeOffset BuildDate { get; set; }
        public Uri SourceCodeRepository { get; set; }
        public string MachineName { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset LastHeartbeat { get; set; }

        [Obsolete("For serialization only")]
        public ServiceInstance() { }

        public ServiceInstance(string host, string name, string service, string machineName, DateTimeOffset startedAt, DateTimeOffset lastHeartbeat, AssemblyInformation info)
            : base(host, name, lastHeartbeat)
        {
            Service = service;
            MachineName = machineName;
            StartedAt = startedAt;
            LastHeartbeat = lastHeartbeat;

            BuildCommit = info.BuildCommit;
            BuildBranch = info.BuildBranch;
            BuildDate = info.BuildDate;
            SourceCodeRepository = info.SourceCodeRepository;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGet.Services.Jobs.Monitoring
{
    public class JobsEntry : TableEntity
    {
        public string Name
        {
            get { return RowKey; }
            set { RowKey = value; }
        }

        [Obsolete("For serialization only")]
        public JobsEntry() { }

        public JobsEntry(string name) : base(partitionKey: String.Empty, rowKey: name) {
            Timestamp = DateTimeOffset.UtcNow;
        }

        public string Runtime { get; set; }
        public string Description { get; set; }
        public bool? Enabled { get; set; }
        public string EventProviderId { get; set; }

        public static JobsEntry ForJob(JobDescription job)
        {
            return new JobsEntry(job.Name)
            {
                Runtime = job.Runtime,
                Description = job.Description,
                EventProviderId = job.EventProvider == null ? 
                    null : 
                    EventSource.GetGuid(job.EventProvider)
                        .ToString()
                        .ToLowerInvariant(),
                Enabled = null
            };
        }
    }
}

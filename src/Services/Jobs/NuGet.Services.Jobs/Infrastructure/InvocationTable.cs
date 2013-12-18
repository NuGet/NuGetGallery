using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Storage;

namespace NuGet.Services.Jobs.Infrastructure
{
    public class InvocationTable
    {
        private AzureTable<Invocation> _masterList;
        private AzureTable<Invocation> _byStatus;
        private AzureTable<Invocation> _byJob;
        private AzureTable<Invocation> _byInstance;
        private Clock _clock;

        public InvocationTable(Clock clock, StorageHub storage)
        {
            _clock = clock;
            _masterList = storage.Primary.Tables.Table<Invocation>();
            _byStatus = storage.Primary.Tables.Table<Invocation>("InvocationsByStatus");
            _byJob = storage.Primary.Tables.Table<Invocation>("InvocationsByJob");
            _byInstance = storage.Primary.Tables.Table<Invocation>("InvocationsByInstance");
        }

        public Task<Invocation> Get(Guid invocationId)
        {
            return _masterList.Get(
                invocationId.ToString("N").ToLowerInvariant(),
                String.Empty);
        }

        internal Task Update(Invocation invocation)
        {
            var id = invocation.Id.ToString("N").ToLowerInvariant();
            var timeKey = ReverseChronologicalRowKey.Create(_clock.UtcNow, id);
            
            // Create pivots
            var master = invocation;
            var byStatus = invocation.Clone();
            var byJob = invocation.Clone();
            var byInstance = invocation.Clone();

            // Set master entry keys
            master.PartitionKey = id;
            master.RowKey = String.Empty;

            // Set by status keys
            byStatus.PartitionKey = byStatus.Status.ToString();
            byStatus.RowKey = timeKey;

            // Set by job keys
            byJob.PartitionKey = byJob.Job;
            byJob.RowKey = timeKey;

            // Set by instance keys
            byInstance.PartitionKey = byInstance.LastInstanceName ?? "";
            byInstance.RowKey = timeKey;

            return Task.WhenAll(
                _masterList.Merge(master),
                _byStatus.Merge(byStatus),
                _byJob.Merge(byJob),
                _byInstance.Merge(byInstance));
        }
    }
}

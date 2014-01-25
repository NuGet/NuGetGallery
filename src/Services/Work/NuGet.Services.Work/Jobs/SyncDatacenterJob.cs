using NuGet.Services.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Jobs
{
    [Description("Sync the secondary datacenter with the primary datacenter")]
    public class SyncDatacenterJob : DatabaseJobHandlerBase<SyncDatacenterEventSource>
    {
        public SyncDatacenterJob(ConfigurationHub configHub) : base(configHub) { }

        protected internal override Task<JobContinuation> Execute()
        {
            // Load Defaults
            var sourceDatacenter = Config.PrimaryDatacenter;
            var destDatacenter = Config.PrimaryDatacenter;

            var sourceServer = Config.Sql.GetConnectionString(KnownSqlServer.Legacy);
            var destServer = Config.Sql.

            Log.PreparingToExport(Config.PrimaryDatacenter, Config.Sql.Legacy, "NuGetGallery");
            var parameters = new Dictionary<string, string>();
            return Task.FromResult<JobContinuation>(Suspend(TimeSpan.FromSeconds(3), parameters));
        }

        protected internal override Task<JobContinuation> Resume()
        {
            return base.Resume();
        }
    }

    public class SyncDatacenterEventSource : EventSource
    {
        public static readonly SyncDatacenterEventSource Log = new SyncDatacenterEventSource();

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Message = "Preparing to export source database {0} on server {1} from primary datacenter {2}")]
        public void PreparingToExport(string datacenter, string server, string database) { WriteEvent(1, database, server, datacenter); }

        private SyncDatacenterEventSource() { }
    }
}

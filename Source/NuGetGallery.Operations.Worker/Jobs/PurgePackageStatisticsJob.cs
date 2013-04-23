using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;

namespace NuGetGallery.Operations.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public class PurgePackageStatisticsJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(30);
            }
        }

        public override TimeSpan Offset
        {
            get
            {
                return TimeSpan.FromMinutes(5);
            }
        }

        public override void RunOnce()
        {
            Logger.Trace("Starting purge package statistics task.");
            new PurgePackageStatisticsTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                WarehouseConnectionString = new SqlConnectionStringBuilder(Settings.WarehouseConnectionString),
                WhatIf = Settings.WhatIf
            }.Execute();
            Logger.Trace("Finished purge package statistics task.");
        }
    }
}

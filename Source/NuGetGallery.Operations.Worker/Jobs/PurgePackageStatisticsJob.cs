using System;
using System.ComponentModel.Composition;

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
                ConnectionString = Settings.MainConnectionString,
                WarehouseConnectionString = Settings.WarehouseConnectionString,
                WhatIf = Settings.WhatIf
            }.Execute();
            Logger.Trace("Finished purge package statistics task.");
        }
    }
}

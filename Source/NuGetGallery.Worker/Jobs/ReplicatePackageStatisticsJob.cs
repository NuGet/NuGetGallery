using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using System.Threading;
using NuGetGallery.Operations;

namespace NuGetGallery.Worker.Jobs
{
    //[Export(typeof(WorkerJob))]
    public class ReplicatePackageStatisticsJob : WorkerJob
    {
        CancellationTokenSource _cts = new CancellationTokenSource();

        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromSeconds(30);
            }
        }

        public override TimeSpan Offset
        {
            get
            {
                return TimeSpan.FromSeconds(30);
            }
        }

        public override void RunOnce()
        {
            Logger.Trace("Starting replicate package statistics task.");
            ReplicatePackageStatisticsTask task = new ReplicatePackageStatisticsTask()
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                WarehouseConnectionString = new SqlConnectionStringBuilder(Settings.WarehouseConnectionString),
                WhatIf = Settings.WhatIf,
                CancellationToken = _cts.Token
            };
            task.Execute();

            StatusMessage = string.Format("replicated {0} download records", task.Count);

            Logger.Trace("Finished replicate package statistics task.");
        }
    }
}

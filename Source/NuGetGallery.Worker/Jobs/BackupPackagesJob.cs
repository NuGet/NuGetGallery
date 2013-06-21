using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations;

namespace NuGetGallery.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public class BackupPackagesJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromHours(1);
            }
        }

        public override TimeSpan Offset
        {
            get
            {
                return TimeSpan.FromMinutes(30);
            }
        }

        public override void RunOnce()
        {
            Logger.Info("Starting backup packages task.");
            ExecuteTask(new BackupPackagesTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                StorageAccount = Settings.MainStorage,
                WhatIf = Settings.WhatIf
            });
            Logger.Info("Finished backup packages task.");
        }
    }
}

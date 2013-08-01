using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations;
using NuGetGallery.Operations.Tasks;
using NuGetGallery.Operations.Tasks.Backups;

namespace NuGetGallery.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public class BackupWarehouseJob : WorkerJob
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
            Logger.Info("Running Warehouse Backup job");

            var warehouse = new SqlConnectionStringBuilder(Settings.WarehouseConnectionString);
            ExecuteTask(new BackupWarehouseTask
            {
                ConnectionString = warehouse,
                WhatIf = Settings.WhatIf,
                IfOlderThan = 60,
                DoNotPoll = false // Warehouse backups take a while...
            });

            Logger.Info("Complete Warehouse Backup job");
        }
    }
}

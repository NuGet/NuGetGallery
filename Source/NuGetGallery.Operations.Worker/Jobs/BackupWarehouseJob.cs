using System;
using System.ComponentModel.Composition;

namespace NuGetGallery.Operations.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public class BackupWarehouseJob : BackupDatabaseBaseJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromDays(1);
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
            Logger.Info("Starting backup warehouse task.");
         
            BackupWarehouseTask backupTask = new BackupWarehouseTask
            {
                ConnectionString = Settings.WarehouseConnectionString,
                WhatIf = Settings.WhatIf
            };

            backupTask.Execute();

            WaitForCompletion(backupTask);

            Logger.Info("Finished backup warehouse task.");
        }
    }
}

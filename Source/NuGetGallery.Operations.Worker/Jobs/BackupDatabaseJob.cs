using System;
using System.ComponentModel.Composition;
using System.Threading;

namespace NuGetGallery.Operations.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public class BackupDatabaseJob : BackupDatabaseBaseJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(30);
            }
        }

        public override void RunOnce()
        {
            Logger.Info("Starting backup database task.");

            BackupDatabaseTask backupTask = new BackupDatabaseTask
            {
                ConnectionString = Settings.MainConnectionString,
                WhatIf = Settings.WhatIf
            };

            backupTask.Execute();

            WaitForCompletion(backupTask);

            Logger.Info("Finished backup database task.");
        }
    }
}

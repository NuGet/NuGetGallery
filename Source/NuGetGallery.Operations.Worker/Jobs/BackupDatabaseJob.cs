using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;

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

            var backupTask = new BackupDatabaseTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                WhatIf = Settings.WhatIf,
                IfOlderThan = 25,
            };

            backupTask.Execute();

            WaitForCompletion(backupTask);

            Logger.Info("Finished backup database task.");
        }
    }
}

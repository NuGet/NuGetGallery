using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;

namespace NuGetGallery.Operations.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public class CleanDatabaseBackupsJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(45);
            }
        }

        public override void RunOnce()
        {
            Logger.Info("Starting delete old database backup task.");
            new CleanOnlineDatabaseBackupsTask
            {
                DestinationStorage = Settings.BackupStorage,
                SqlDacEndpoint = Settings.SqlDac,
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                WhatIf = Settings.WhatIf
            }.Execute();
            Logger.Info("Finished delete old database backup task.");
        }
    }
}

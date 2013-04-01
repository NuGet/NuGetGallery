using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;

namespace NuGetGallery.Operations.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public class DeleteOldWarehouseBackupsJob : WorkerJob
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
            Logger.Info("Starting delete old warehouse backup task.");
            new DeleteOldWarehouseBackupsTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.WarehouseConnectionString),
                WhatIf = Settings.WhatIf
            }.Execute();
            Logger.Info("Finished delete old warehouse backup task.");
        }
    }
}

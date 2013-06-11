using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations;

namespace NuGetGallery.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public class BackupDatabaseJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromSeconds(30);
            }
        }

        public override void RunOnce()
        {
            ExecuteTask(new BackupDatabaseTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                WhatIf = Settings.WhatIf,
                IfOlderThan = 25,
            });
        }
    }
}

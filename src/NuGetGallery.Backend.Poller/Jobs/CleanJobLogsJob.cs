using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations;
using NuGetGallery.Operations.Tasks.Monitoring;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class CleanJobLogsJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromHours(12);
            }
        }

        public override void RunOnce()
        {
            ExecuteTask(new CleanJobLogsTask
            {
                StorageAccount = Settings.DiagnosticsStorage,
                WhatIf = Settings.WhatIf,
                MaxAge = TimeSpan.FromDays(7)
            });
        }
    }
}
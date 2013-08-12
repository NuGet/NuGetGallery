using System;
using System.Data.SqlClient;
using NuGetGallery.Operations.Tasks;

namespace NuGetGallery.Backend.Jobs
{
    class HandleQueuedPackageEditsJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(5);
            }
        }

        public override void RunOnce()
        {
            new HandleQueuedPackageEditsTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                StorageAccount = Settings.MainStorage,
                WhatIf = Settings.WhatIf
            }.Execute();
        }
    }
} 
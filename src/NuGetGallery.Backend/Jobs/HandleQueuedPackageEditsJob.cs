using System;
using System.Data.SqlClient;
using NuGetGallery.Operations.Tasks;

namespace NuGetGallery.Backend.Jobs
{
    internal class HandleQueuedPackageEditsJob : WorkerJob
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
            new HandleQueuedPackageEditsTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                StorageAccount = Settings.MainStorage,
                WhatIf = Settings.WhatIf
            }.Execute();
        }
    }
} 
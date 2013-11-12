using NuGetGallery.Operations.Tasks.Search;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class AddToIndexJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(1);
            }
        }

        public override void RunOnce()
        {
            Logger.Info("Starting IndexPackageAddsTask.");
            ExecuteTask(new IndexPackageAddsTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                StorageAccount = Settings.MainStorage,
                Container = "index",
                WhatIf = Settings.WhatIf
            });
            Logger.Info("Finished IndexPackageAddsTask.");
        }
    }
}

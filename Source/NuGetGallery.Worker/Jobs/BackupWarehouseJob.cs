using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations;
using NuGetGallery.Operations.Tasks;
using NuGetGallery.Operations.Tasks.Backups;

namespace NuGetGallery.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public class BackupWarehouseJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(20);
            }
        }

        public override TimeSpan Offset
        {
            get
            {
                return TimeSpan.FromMinutes(5);
            }
        }

        public override void RunOnce()
        {
            Logger.Info("Running Warehouse Backup job");

            var warehouse = new SqlConnectionStringBuilder(Settings.WarehouseConnectionString);
            ExecuteTask(new BackupWarehouseTask
            {
                ConnectionString = warehouse,
                WhatIf = Settings.WhatIf,
                IfOlderThan = 25,
            });

            // Run the export and clean tasks next
            ExecuteTask(new ExportWarehouseBackupsTask()
            {
                ConnectionString = warehouse,
                StorageAccount = Settings.BackupStorage,
                WhatIf = Settings.WhatIf,
                SqlDacEndpoint = Settings.SqlDac
            });

            ExecuteTask(new CleanWarehouseBackupsTask()
            {
                ConnectionString = warehouse,
                WhatIf = Settings.WhatIf,
            });

            Logger.Info("Complete Warehouse Backup job");
        }
    }
}

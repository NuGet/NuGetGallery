using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Operations.Tasks;
using NuGetGallery.Operations.Tasks.Backups;

namespace NuGetGallery.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public class ExportWarehouseBackupsJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(30);
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
            Logger.Info("Running Warehouse Backup Export job");

            // Run the export and clean tasks next
            var warehouse = new SqlConnectionStringBuilder(Settings.WarehouseConnectionString);
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

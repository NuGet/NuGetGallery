// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations;
using NuGetGallery.Operations.Tasks;
using NuGetGallery.Operations.Tasks.Backups;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class BackupWarehouseJob : WorkerJob
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
            Logger.Info("Running Warehouse Backup job");

            var warehouse = new SqlConnectionStringBuilder(Settings.WarehouseConnectionString);
            ExecuteTask(new BackupWarehouseTask
            {
                ConnectionString = warehouse,
                WhatIf = Settings.WhatIf,
                IfOlderThan = 60,
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
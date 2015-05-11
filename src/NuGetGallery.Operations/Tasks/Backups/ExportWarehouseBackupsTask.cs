// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Operations.Common;
using NuGetGallery.Operations.Model;

namespace NuGetGallery.Operations.Tasks
{
    [Command("exportwarehousebackups", "Exports the daily backup for each day to Blob storage", AltName = "xwb", IsSpecialPurpose = true)]
    public class ExportWarehouseBackupsTask : WarehouseTask
    {
        [Option("The storage account in which to place the backup", AltName = "s")]
        public CloudStorageAccount StorageAccount { get; set; }

        [Option("The URL of the SQL DAC Endpoint", AltName="dac")]
        public Uri SqlDacEndpoint { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (CurrentEnvironment != null)
            {
                if (StorageAccount == null)
                {
                    StorageAccount = CurrentEnvironment.BackupStorage;
                }
                if (SqlDacEndpoint == null)
                {
                    SqlDacEndpoint = CurrentEnvironment.SqlDacEndpoint;
                }
            }

            ArgCheck.RequiredOrConfig(StorageAccount, "StorageAccount");
            ArgCheck.RequiredOrConfig(SqlDacEndpoint, "SqlDacEndpoint");
        }

        public override void ExecuteCommand()
        {
            WithMasterConnection((connection, db) =>
            {
                // Get the list of database backups
                var backups = db.Query<Db>(
                    "SELECT name, state FROM sys.databases WHERE name LIKE 'WarehouseBackup_%' AND state = @state",
                    new { state = Util.OnlineState })
                    .Select(d => new OnlineDatabaseBackup(Util.GetDatabaseServerName(ConnectionString), d.Name, d.State))
                    .Where(b => b.Timestamp != null)
                    .OrderByDescending(b => b.Timestamp)
                    .ToList();

                // Grab any end-of-day backups
                var dailyBackups = backups
                    .Where(b => b.Timestamp.Value.Hour == 23 && b.Timestamp.Value.Minute > 30)
                    .ToList();
                Log.Info("Found {0} daily backups to export", dailyBackups.Count);

                // Start exporting them
                foreach (var dailyBackup in dailyBackups)
                {
                    Log.Info("Exporting '{0}'...", dailyBackup.DatabaseName);
                    (new ExportDatabaseTask()
                    {
                        ConnectionString = new SqlConnectionStringBuilder(ConnectionString.ConnectionString)
                        {
                            InitialCatalog = dailyBackup.DatabaseName
                        },
                        DestinationStorage = StorageAccount,
                        DestinationContainer = "warehouse-backups",
                        SqlDacEndpoint = SqlDacEndpoint,
                        WhatIf = WhatIf
                    }).Execute();
                }
            });
        }
    }
}

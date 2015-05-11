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
    [Command("exportdailybackups", "Exports the daily backup for each day to Blob storage", AltName = "xddb", IsSpecialPurpose = true)]
    public class ExportDailyBackupsTask : DatabaseTask
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
            var cstr = Util.GetMasterConnectionString(ConnectionString.ConnectionString);
            using (var connection = new SqlConnection(cstr))
            using (var db = new SqlExecutor(connection))
            {
                connection.Open();

                // Snap the current date just in case we are running right on the cusp
                var today = DateTime.UtcNow;

                // Get the list of database backups
                var backups = db.Query<Db>(
                    "SELECT name, state FROM sys.databases WHERE name LIKE 'Backup_%'")
                    .Select(d => new OnlineDatabaseBackup(Util.GetDatabaseServerName(ConnectionString), d.Name, d.State))
                    .OrderByDescending(b => b.Timestamp)
                    .ToList();

                // Grab end-of-day backups from days before today
                var dailyBackups = backups
                    .GroupBy(b => b.Timestamp.Value.Date)
                    .Where(g => g.Key < today.Date)
                    .Select(g => g.OrderByDescending(b => b.Timestamp.Value).Last())
                    .ToList();
                Log.Info("Found {0} daily backups to export", dailyBackups.Count);

                // Start exporting them
                foreach (var dailyBackup in dailyBackups)
                {
                    if (dailyBackup.State != Util.OnlineState)
                    {
                        Log.Info("Skipping '{0}', it is still being copied", dailyBackup.DatabaseName);
                    }
                    else
                    {
                        if (dailyBackup.Timestamp.Value.TimeOfDay < new TimeSpan(23, 30, 00))
                        {
                            Log.Warn("Somehow, '{0}' is the only backup from {1}. Exporting it to be paranoid",
                                dailyBackup.DatabaseName,
                                dailyBackup.Timestamp.Value.Date.ToShortDateString());
                        }
                        Log.Info("Exporting '{0}'...", dailyBackup.DatabaseName);
                        (new ExportDatabaseTask()
                        {
                            ConnectionString = new SqlConnectionStringBuilder(ConnectionString.ConnectionString)
                            {
                                InitialCatalog = dailyBackup.DatabaseName
                            },
                            DestinationStorage = StorageAccount,
                            DestinationContainer = "database-backups",
                            SqlDacEndpoint = SqlDacEndpoint,
                            WhatIf = WhatIf
                        }).Execute();
                    }
                }
            }
        }
    }
}

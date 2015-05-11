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
    [Command("cleanonlinebackups", "Cleans up online database backups", AltName="clodb")]
    public class CleanOnlineDatabaseBackupsTask : DatabaseTask
    {
        [Option("The storage account containing offline database backups (.bacpac files)", AltName="st")]
        public CloudStorageAccount BackupStorage { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (BackupStorage == null && CurrentEnvironment != null)
            {
                BackupStorage = CurrentEnvironment.BackupStorage;
            }

            ArgCheck.RequiredOrConfig(BackupStorage, "BackupStorage");
        }

        public override void ExecuteCommand()
        {
            // The backup policy:
            //  1. Keep 2 rolling 30 minute backups
            //  2. Keep the last backup (from 23:30 - 23:59) of the past two days
            //  3. Delete any backup from before the past two days
            
            // The result is:
            //  1 Active Database
            //  1 30min-old Backup
            //  1 60min-old Backup
            //  1 24hr-old (at most) Backup
            //  1 48hr-old (at most) Backup

            // TODO: Parameterize the policy (i.e. BackupPeriod, RollingBackupCount, DailyBackupCount, etc.)

            var cstr = Util.GetMasterConnectionString(ConnectionString.ConnectionString);
            using (var connection = new SqlConnection(cstr))
            using (var db = new SqlExecutor(connection))
            {
                connection.Open();

                // Get the list of backups
                var backups = db.Query<Db>(
                    "SELECT name, state FROM sys.databases WHERE name LIKE 'Backup_%'",
                    new { state = Util.OnlineState })
                    .Select(d => new OnlineDatabaseBackup(Util.GetDatabaseServerName(ConnectionString), d.Name, d.State))
                    .OrderByDescending(b => b.Timestamp)
                    .ToList();

                // Any currently copying are safe
                var keepers = new HashSet<string>();
                keepers.AddRange(backups.Where(b => b.State == Util.CopyingState).Select(b => b.DatabaseName));
                
                // The last 2 online databases are definitely safe
                keepers.AddRange(backups.Where(b => b.State == Util.OnlineState).Take(2).Select(b => b.DatabaseName));
                Log.Info("Selected most recent two backups: {0}", String.Join(", ", keepers));

                // Group by day, and skip any from today
                var days = backups
                    .GroupBy(b => b.Timestamp.Value.Date)
                    .OrderByDescending(g => g.Key)
                    .Where(g => g.Key < DateTime.UtcNow.Date); // .Date gives us the current day at 00:00 hours, so "<" means previous day and earlier
    
                // Keep the last backup from each of the previous two days
                var dailyBackups = days
                    .Take(2) // Grab the last two days
                    .Select(day => day.OrderByDescending(b => b.Timestamp.Value).First()); // The last backup from each day
                Log.Info("Selected most recent two daily backups: {0}", String.Join(", ", dailyBackups.Select(b => b.DatabaseName)));

                // Verify data
                var brokenDays = dailyBackups.Where(b => b.Timestamp.Value.TimeOfDay < new TimeSpan(23, 30, 00));
                if(brokenDays.Any()) {
                    foreach(var brokenDay in brokenDays) {
                        Log.Warn("Daily backups for {0} are from earlier than 23:30 hours?", brokenDay.Timestamp.Value.DateTime.ToShortDateString());
                    }
                }
                var exportedDailyBackups = days.Skip(2).Select(day => day.Last());
                var client = BackupStorage.CreateCloudBlobClient();
                var container = client.GetContainerReference("database-backups");
                foreach (var exportedDaily in exportedDailyBackups)
                {
                    // We should be able to find a backup blob
                    string blobName = exportedDaily.DatabaseName + ".bacpac";
                    var blob = container.GetBlockBlobReference(blobName);
                    if (!blob.Exists())
                    {
                        // Derp?
                        Log.Warn("Expected {0} blob to exist but it hasn't been exported!", blob.Name);
                        keepers.Add(exportedDaily.DatabaseName); // Keep it for now.
                    }
                }

                // Keep those backups!
                keepers.AddRange(dailyBackups.Select(b => b.DatabaseName));

                // Figure out how many we're keeping
                Log.Info("Keeping the following Backups: {0}", String.Join(", ", keepers));

                if (keepers.Count < 2)
                {
                    // Abort!
                    Log.Warn("About to clean too many backups. Aborting until we have enough to be in-policy.");
                }
                else
                {
                    // Delete the rest!
                    foreach (var backup in backups.Where(b => !keepers.Contains(b.DatabaseName)))
                    {
                        if (!WhatIf)
                        {
                            db.Execute("DROP DATABASE " + backup.DatabaseName);
                        }
                        Log.Info("Deleted {0}", backup.DatabaseName);
                    }
                }
            }
        }
    }
}

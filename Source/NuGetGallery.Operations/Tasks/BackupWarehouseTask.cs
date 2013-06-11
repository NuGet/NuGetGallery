using System;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Common;
using NuGetGallery.Operations.Infrastructure;

namespace NuGetGallery.Operations
{
    [Command("backupwarehouse", "Backs up the warehouse", AltName = "bwh", MaxArgs = 0)]
    public class BackupWarehouseTask : WarehouseTask, IAsyncCompletionTask
    {
        [Option("Backup should occur if the database is older than X minutes (default 30 minutes)")]
        public int IfOlderThan { get; set; }

        public string BackupName { get; private set; }

        public bool SkippingBackup { get; private set; }

        public BackupWarehouseTask()
        {
            IfOlderThan = 30;
        }

        public override void ExecuteCommand()
        {
            var dbServer = ConnectionString.DataSource;
            var dbName = ConnectionString.InitialCatalog;
            var masterConnectionString = Util.GetMasterConnectionString(ConnectionString.ConnectionString);

            Log.Trace("Connecting to server '{0}' to back up database '{1}'.", dbServer, dbName);

            using (var sqlConnection = new SqlConnection(masterConnectionString))
            {
                sqlConnection.Open();

                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    Log.Trace("Checking for a backup in progress.");
                    if (Util.BackupIsInProgress(dbExecutor))
                    {
                        Log.Trace("Found a backup in progress; exiting.");
                        return;
                    }

                    Log.Trace("Found no backup in progress.");

                    Log.Trace("Getting last backup time.");
                    var lastBackupTime = Util.GetLastBackupTime(dbExecutor);
                    if (lastBackupTime >= DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(IfOlderThan)))
                    {
                        Log.Info("Skipping Backup. Last Backup was less than {0} minutes ago", IfOlderThan);

                        SkippingBackup = true;

                        return;
                    }
                    Log.Trace("Last backup time is more than {0} minutes ago. Starting new backup.", IfOlderThan);

                    var timestamp = Util.GetTimestamp();

                    BackupName = string.Format("WarehouseBackup_{0}", timestamp);

                    if (!WhatIf)
                    {
                        dbExecutor.Execute(string.Format("CREATE DATABASE {0} AS COPY OF {1}", BackupName, dbName));
                    }

                    Log.Info("Starting '{0}'", BackupName);
                }
            }
        }

        public TimeSpan RecommendedPollingPeriod
        {
            get { return TimeSpan.FromMinutes(1); }
        }

        public TimeSpan MaximumPollingLength
        {
            get { return TimeSpan.FromMinutes(45); }
        }

        public bool PollForCompletion()
        {
            return DatabaseBackupHelper.GetBackupStatus(Log, ConnectionString, BackupName);
        }
    }
}

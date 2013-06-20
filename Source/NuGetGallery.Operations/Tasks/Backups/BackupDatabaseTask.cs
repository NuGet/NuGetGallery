using System;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Infrastructure;

namespace NuGetGallery.Operations
{
    [Command("backupdatabase", "Backs up the database", AltName = "bdb", MaxArgs = 0)]
    public class BackupDatabaseTask : DatabaseTask, IAsyncCompletionTask
    {
        private bool _startedBackup = false;

        [Option("Backup should occur if the database is older than X minutes (default 30 minutes)")]
        public int IfOlderThan { get; set; }

        [Option("The name of the backup to created. Default: Backup_[yyyyMMddHHmmss]")]
        public string BackupName { get; set; }

        [Option("Forces the backup to be created, even if there is a recent enough backup.")]
        public bool Force { get; set; }

        public BackupDatabaseTask()
        {
            IfOlderThan = 30;
        }

        public override void ExecuteCommand()
        {
            Log.Trace("Connecting to server '{0}' to back up database '{1}'.", DatabaseName, ServerName);

            _startedBackup = false;

            WithMasterConnection((connection, db) =>
            {
                connection.Open();

                if (!Force)
                {
                    Log.Trace("Checking for a backup in progress.");
                    if (Util.BackupIsInProgress(db))
                    {
                        Log.Trace("Found a backup in progress; exiting.");
                        return;
                    }

                    Log.Trace("Found no backup in progress.");

                    Log.Trace("Getting last backup time.");
                    var lastBackupTime = Util.GetLastBackupTime(db);
                    if (lastBackupTime >= DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(IfOlderThan)))
                    {
                        Log.Info("Skipping Backup. Last Backup was less than {0} minutes ago", IfOlderThan);
                        return;
                    }
                    Log.Trace("Last backup time is more than {0} minutes ago. Starting new backup.", IfOlderThan);
                }
                else
                {
                    Log.Trace("Forcing new backup");
                }

                if (String.IsNullOrEmpty(BackupName))
                {
                    // Generate a backup name
                    var timestamp = Util.GetTimestamp();

                    BackupName = string.Format("Backup_{0}", timestamp);
                }

                if (!WhatIf)
                {
                    db.Execute(string.Format("CREATE DATABASE {0} AS COPY OF {1}", BackupName, DatabaseName));
                    _startedBackup = true;
                }

                Log.Info("Started Copy of '{0}' to '{1}'", DatabaseName, BackupName);
            });
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
            return !_startedBackup || DatabaseBackupHelper.GetBackupStatus(Log, ConnectionString, BackupName);
        }
    }
}

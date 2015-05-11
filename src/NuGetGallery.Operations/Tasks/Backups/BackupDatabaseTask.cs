// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Infrastructure;

namespace NuGetGallery.Operations
{
    [Command("backupdatabase", "Backs up the database", AltName = "bdb", MaxArgs = 0)]
    public class BackupDatabaseTask : OpsTask, IAsyncCompletionTask
    {
        private bool _startedBackup = false;

        [Option("Backup should occur if the database is older than X minutes (default 30 minutes)")]
        public int IfOlderThan { get; set; }

        [Option("The prefix to apply to the backup name (the suffix is a timestamp)")]
        public string BackupNamePrefix { get; set; }

        [Option("Forces the backup to be created, even if there is a recent enough backup.")]
        public bool Force { get; set; }

        [Option("The connection to the database to backup", AltName = "db")]
        public SqlConnectionStringBuilder ConnectionString { get; set; }

        private string _backupName;

        public BackupDatabaseTask()
        {
            IfOlderThan = 30;
        }

        public override void ValidateArguments()
        {
            if (ConnectionString == null && CurrentEnvironment != null)
            {
                ConnectionString = SelectEnvironmentConnection(CurrentEnvironment);
            }

            BackupNamePrefix = BackupNamePrefix ?? "Backup_";
        }

        public override void ExecuteCommand()
        {
            Log.Trace("Connecting to server '{0}' to back up database '{1}'.", ConnectionString.InitialCatalog, Util.GetDatabaseServerName(ConnectionString));

            _startedBackup = false;

            var cstr = Util.GetMasterConnectionString(ConnectionString.ConnectionString);
            using(var connection = new SqlConnection(cstr))
            using(var db = new SqlExecutor(connection))
            {
                connection.Open();

                if (!Force)
                {
                    Log.Trace("Checking for a backup in progress.");
                    if (Util.BackupIsInProgress(db, BackupNamePrefix))
                    {
                        Log.Trace("Found a backup in progress; exiting.");
                        return;
                    }

                    Log.Trace("Found no backup in progress.");

                    Log.Trace("Getting last backup time.");
                    var lastBackupTime = Util.GetLastBackupTime(db, BackupNamePrefix);
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

                // Generate a backup name
                var timestamp = Util.GetTimestamp();

                _backupName = BackupNamePrefix + timestamp;

                if (!WhatIf)
                {
                    db.Execute(string.Format("CREATE DATABASE {0} AS COPY OF {1}", _backupName, ConnectionString.InitialCatalog));
                    _startedBackup = true;
                }

                Log.Info("Started Copy of '{0}' to '{1}'", ConnectionString.InitialCatalog, _backupName);
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
            return !_startedBackup || DatabaseBackupHelper.GetBackupStatus(Log, ConnectionString, _backupName);
        }

        protected virtual SqlConnectionStringBuilder SelectEnvironmentConnection(DeploymentEnvironment env)
        {
            return env.MainDatabase;
        }
    }
}

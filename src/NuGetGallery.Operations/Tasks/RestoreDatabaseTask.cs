// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Data.SqlClient;
using System.Threading;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("restoredb", "Restore a database backup which already resides on the specified database server", AltName = "rdb")]
    public class RestoreDatabaseTask : DatabaseTask
    {
        [Option("Name of the backup file", AltName = "n")]
        public string BackupName { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.Required(BackupName, "BackupName");
        }

        public override void ExecuteCommand()
        {
            using (var masterDbConnection = new SqlConnection(Util.GetMasterConnectionString(ConnectionString.ConnectionString)))
            using (var masterDbExecutor = new SqlExecutor(masterDbConnection))
            {
                masterDbConnection.Open();
                
                var restoreDbName = CopyDatabaseForRestore(
                    masterDbExecutor);

                using (var restoreDbConnection = new SqlConnection(Util.GetConnectionString(ConnectionString.ConnectionString, restoreDbName)))
                using (var restoreDbExecutor = new SqlExecutor(restoreDbConnection))
                {
                    restoreDbConnection.Open();

                    PrepareDataForRestore(
                        restoreDbExecutor);

                    RenameLiveDatabase(
                        masterDbExecutor);

                    RenameDatabaseBackup(
                        masterDbExecutor,
                        restoreDbName);
                }
            }
        }

        private string CopyDatabaseForRestore(
            SqlExecutor masterDbExecutor)
        {
            var restoreDbName = string.Format("Restore_{0}", Util.GetTimestamp());
            Log.Info("Copying {0} to {1}.", BackupName, restoreDbName);
            masterDbExecutor.Execute(string.Format("CREATE DATABASE {0} AS COPY OF {1}", restoreDbName, BackupName));
            Log.Info("Waiting for copy to complete.");
            WaitForBackupCopy(
                masterDbExecutor,
                restoreDbName);
            return restoreDbName;
        }

        private void WaitForBackupCopy(
            SqlExecutor masterDbExecutor,
            string restoreDbName)
        {
            var timeToGiveUp = DateTime.UtcNow.AddHours(1).AddSeconds(30);
            while (DateTime.UtcNow < timeToGiveUp)
            {
                if (Util.DatabaseExistsAndIsOnline(
                    masterDbExecutor,
                    restoreDbName))
                {
                    Log.Info("Copy is complete.");
                    return;
                }
                Thread.Sleep(1 * 60 * 1000);
            }
        }

        private void PrepareDataForRestore(
            IDbExecutor dbExecutor)
        {
            Log.Info("Deleting incomplete jobs.");
            dbExecutor.Execute("DELETE FROM WorkItems WHERE Completed IS NULL");
            Log.Info("Deleted incomplete jobs.");
        }

        private void RenameDatabaseBackup(
            IDbExecutor masterDbExecutor,
            string restoreDbName)
        {
            Log.Info("Renaming {0} to NuGetGallery.", restoreDbName);
            var sql = string.Format("ALTER DATABASE {0} MODIFY Name = NuGetGallery", restoreDbName);
            masterDbExecutor.Execute(sql);
            Log.Info("Renamed {0} to NuGetGallery.", restoreDbName);
        }

        private void RenameLiveDatabase(
            IDbExecutor masterDbExecutor)
        {
            var timestamp = Util.GetTimestamp();
            var liveDbName = "Live_" + timestamp;
            Log.Info("Renaming NuGetGallery to {0}.", liveDbName);
            var sql = string.Format("ALTER DATABASE NuGetGallery MODIFY Name = {0}", liveDbName);
            masterDbExecutor.Execute(sql);
            Log.Info("Renamed NuGetGallery to {0}.", liveDbName);
        }
    }
}

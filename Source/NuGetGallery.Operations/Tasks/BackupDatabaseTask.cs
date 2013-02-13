using System;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;

namespace NuGetGallery.Operations
{
    [Command("backupdatabase", "Backs up the database", AltName = "bdb", MaxArgs = 0)]
    public class BackupDatabaseTask : DatabaseTask, IBackupDatabase
    {
        [Option("Force a backup, even if there is one less than 24 hours old", AltName="f")]
        public bool Force { get; set; }

        public string BackupName { get; private set; }

        public bool SkippingBackup { get; private set; }

        public override void ExecuteCommand()
        {
            var dbServer = Util.GetDbServer(ConnectionString);
            var dbName = Util.GetDbName(ConnectionString);
            var masterConnectionString = Util.GetMasterConnectionString(ConnectionString);

            Log.Trace("Connecting to server '{0}' to back up database '{1}'.", dbServer, dbName);

            SkippingBackup = false;

            using (var sqlConnection = new SqlConnection(masterConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                Log.Trace("Checking for a backup in progress.");
                if (Util.BackupIsInProgress(dbExecutor))
                {
                    Log.Trace("Found a backup in progress; exiting.");
                    return;
                }

                Log.Trace("Found no backup in progress.");

                if (!Force)
                {
                    Log.Trace("Getting last backup time.");
                    var lastBackupTime = Util.GetLastBackupTime(dbExecutor);
                    if (lastBackupTime >= DateTime.UtcNow.Subtract(TimeSpan.FromHours(24)))
                    {
                        Log.Info("Skipping Backup. Last Backup was less than 24 hours ago");

                        SkippingBackup = true;

                        return;
                    }
                    Log.Trace("Last backup time is more than 24 hours ago. Starting new backup.");
                }

                var timestamp = Util.GetTimestamp();

                BackupName = string.Format("Backup_{0}", timestamp);

                dbExecutor.Execute(string.Format("CREATE DATABASE {0} AS COPY OF NuGetGallery", BackupName));

                Log.Info("Starting '{0}'", BackupName);
            }
        }
    }
}

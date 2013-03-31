using System;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("backupwarehouse", "Backs up the warehouse", AltName = "bwh", MaxArgs = 0)]
    public class BackupWarehouseTask : OpsTask, IBackupDatabase
    {
        [Option("Backup should occur if the database is older than X minutes (default 30 minutes)")]
        public int IfOlderThan { get; set; }

        [Option("Connection string to the warehouse database server", AltName = "wdb")]
        public SqlConnectionStringBuilder ConnectionString { get; set; }

        public string BackupName { get; private set; }

        public bool SkippingBackup { get; private set; }

        public BackupWarehouseTask()
        {
            // Load defaults from environment
            var connectionString = Environment.GetEnvironmentVariable("NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING");
            ConnectionString = String.IsNullOrEmpty(connectionString) ? null : new SqlConnectionStringBuilder(connectionString);
            IfOlderThan = 30;
        }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.RequiredOrEnv(ConnectionString, "ConnectionString", "NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING");
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

                    dbExecutor.Execute(string.Format("CREATE DATABASE {0} AS COPY OF NuGetWarehouse", BackupName));

                    Log.Info("Starting '{0}'", BackupName);
                }
            }
        }
    }
}

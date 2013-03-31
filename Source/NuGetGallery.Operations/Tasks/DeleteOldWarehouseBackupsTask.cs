using System;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations
{
    [Command("purgewarehousebackups", "Deletes old database backups", AltName = "pwh")]
    public class DeleteOldWarehouseBackupsTask : OpsTask
    {
        [Option("Connection string to the warehouse database server", AltName = "wdb")]
        public SqlConnectionStringBuilder WarehouseConnectionString { get; set; }

        public DeleteOldWarehouseBackupsTask() 
        {
            // Load defaults from environment
            var connectionString = Environment.GetEnvironmentVariable("NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING");
            WarehouseConnectionString = String.IsNullOrEmpty(connectionString) ? null : new SqlConnectionStringBuilder(connectionString);
        }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            ArgCheck.RequiredOrEnv(WarehouseConnectionString, "WarehouseConnectionString", "NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING");
        }

        public override void ExecuteCommand()
        {
            var dbServer = WarehouseConnectionString.DataSource;
            var masterConnectionString = Util.GetMasterConnectionString(WarehouseConnectionString.ConnectionString);

            Log.Trace("Deleting old warehouse backups for server '{0}':", dbServer);

            using (var sqlConnection = new SqlConnection(masterConnectionString))
            {
                sqlConnection.Open();

                using (var dbExecutor = new SqlExecutor(sqlConnection))
                {
                    var dbs = dbExecutor.Query<Database>(
                        "SELECT name FROM sys.databases WHERE name LIKE 'WarehouseBackup_%' AND state = @state",
                        new { state = Util.OnlineState });

                    foreach (var db in dbs)
                    {
                        var timestamp = Util.GetDatabaseNameTimestamp(db);
                        var date = Util.GetDateTimeFromTimestamp(timestamp);
                        if (DateTime.UtcNow.Subtract(TimeSpan.FromDays(7)) > date)
                            DeleteDatabaseBackup(db, dbExecutor);
                    }
                }
            }
        }

        private void DeleteDatabaseBackup(Database db, SqlExecutor dbExecutor)
        {
            if (!WhatIf)
            {
                dbExecutor.Execute(string.Format("DROP DATABASE {0}", db.Name));
            }
            Log.Info("Deleted database {0}.", db.Name);
        }
    }
}

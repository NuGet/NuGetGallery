using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Model;

namespace NuGetGallery.Operations
{
    [Command("purgedatabasebackups", "Deletes old database backups", AltName = "pdb")]
    public class DeleteOldDatabaseBackupsTask : DatabaseTask
    {
        public override void ExecuteCommand()
        {
            var dbServer = ConnectionString.DataSource;
            var masterConnectionString = Util.GetMasterConnectionString(ConnectionString.ConnectionString);

            Log.Trace("Deleting old database backups for server '{0}':", dbServer);

            using (var sqlConnection = new SqlConnection(masterConnectionString))
            using (var dbExecutor = new SqlExecutor(sqlConnection))
            {
                sqlConnection.Open();

                var dbs = dbExecutor.Query<Database>(
                    "SELECT name FROM sys.databases WHERE name LIKE 'Backup_%' AND state = @state",
                    new { state = Util.OnlineState })
                   .Select(d => new DatabaseBackup(dbServer, d.Name))
                   .Where(d => d.Timestamp.HasValue)
                   .OrderByDescending(d => d.Timestamp)
                   .ToList();

                
                // Get back to the previous midnight
                var yesterday = DateTimeOffset.UtcNow.AddDays(-1d);
                var startPoint = new DateTimeOffset(yesterday.Year, yesterday.Month, yesterday.Day, 23, 59, 59, TimeSpan.Zero);

                // Set up the list of saved DBs
                var dbsToSave = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                // Save all databases after the start point
                dbsToSave
                    .AddRange(dbs
                        .Where(d => d.Timestamp.Value > startPoint)
                        .Select(d => d.DatabaseName));
                dbs = dbs.Where(d => d.Timestamp.Value < startPoint).ToList();

                // Save all databases on the same day as the start point
                dbsToSave
                    .AddRange(dbs
                        .Where(d => (startPoint - d.Timestamp.Value).TotalDays <= 1.0)
                        .Select(d => d.DatabaseName));
                    
                
                if (dbsToSave.Count <= 0)
                {
                    throw new ApplicationException("Abort - sanity check failed - we are about to delete all backups");
                }

                foreach (var db in dbs)
                {
                    if (dbsToSave.Contains(db))
                    {
                        Log.Info("Retained backup: " + db.Name);
                    }
                    else
                    {
                        DeleteDatabaseBackup(db, dbExecutor);
                    }
                }
            }
        }

        private static readonly DateTimeOffset StartPoint = new DateTimeOffset(2000, 1, 1, 0, 0, 0, 0, TimeSpan.Zero);
        private int GetDay(DatabaseBackup db)
        {
            return (db.Timestamp.Value - StartPoint).Days;
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

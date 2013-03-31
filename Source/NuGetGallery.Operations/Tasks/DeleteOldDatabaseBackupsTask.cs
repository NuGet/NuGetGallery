using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using AnglicanGeek.DbExecutor;

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
                    new { state = Util.OnlineState }).ToArray();

                // Policy #1: retain last backup each day for last week [day = UTC day]
                // Policy #2: retain the last 5 backups
                var dailyBackups = dbs.OrderByDescending(GetTimestamp).GroupBy(GetDay).Take(8).Select(Enumerable.Last);
                var latestBackups = dbs.OrderByDescending(GetTimestamp).Take(5);

                var dbsToSave = new HashSet<Database>();
                dbsToSave.UnionWith(dailyBackups);
                dbsToSave.UnionWith(latestBackups);

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

        private static DateTime GetTimestamp(Database db)
        {
            var timestamp = Util.GetDatabaseNameTimestamp(db);
            var date = Util.GetDateTimeFromTimestamp(timestamp);
            return date;
        }

        private static int GetDay(Database db)
        {
            var timestamp = Util.GetDatabaseNameTimestamp(db);
            var date = Util.GetDateTimeFromTimestamp(timestamp);
            if (date.Kind != DateTimeKind.Utc)
            {
                throw new InvalidDataException("DateTime must be Utc");
            }

            var daysSinceMillenium = (int)date.Subtract(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
            return daysSinceMillenium;
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

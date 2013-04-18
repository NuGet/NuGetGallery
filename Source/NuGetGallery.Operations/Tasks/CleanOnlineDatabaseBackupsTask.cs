using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using AnglicanGeek.DbExecutor;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Operations.Common;
using NuGetGallery.Operations.Model;

namespace NuGetGallery.Operations
{
    [Command("cleanonlinebackups", "Deletes online backups that are out of date and pushes offline backups over to blob storage", AltName = "podb")]
    public class CleanOnlineDatabaseBackupsTask : DatabaseTask
    {
        [Option("Azure Storage Account in which the exported database should be placed", AltName = "s")]
        public CloudStorageAccount DestinationStorage { get; set; }

        [Option("URL of the SQL DAC endpoint to talk to", AltName = "dac")]
        public Uri SqlDacEndpoint { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (CurrentEnvironment != null)
            {
                if (DestinationStorage == null)
                {
                    DestinationStorage = CurrentEnvironment.BackupStorage;
                }
                if (SqlDacEndpoint == null)
                {
                    SqlDacEndpoint = CurrentEnvironment.SqlDacEndpoint;
                }
            }

            ArgCheck.RequiredOrConfig(DestinationStorage, "DestinationStorage");
            ArgCheck.RequiredOrConfig(SqlDacEndpoint, "SqlDacEndpoint");
        }

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

                // Take all backups younger than 12 hours
                var now = DateTimeOffset.UtcNow;
                var rollingBackups = dbs.Where(db => (now - db.Timestamp.Value).TotalHours <= 12.0);

                // LINQ ALL THE THINGS!
                // Put backups into day buckets
                var backupDays = from db in dbs
                                 let d = db.Timestamp.Value
                                 group db by new { d.Year, d.Month, d.Day } into g
                                 orderby g.Key.Year descending, 
                                         g.Key.Month descending, 
                                         g.Key.Day descending
                                 select g;

                // Take the last backup from the two buckets AFTER the current one
                // The three buckets should be today, yesterday and the day before. Since this is being run frequently
                var dailyBackups = backupDays
                    .Skip(1)
                    .Take(2)
                    .Select(backups => backups
                        .OrderByDescending(db => db.Timestamp.Value.Hour)
                        .Last());

                // List the backups we're keeping:
                var keepOnline = new HashSet<string>(
                    Enumerable.Concat(
                        rollingBackups.Select(db => db.DatabaseName),
                        dailyBackups.Select(db => db.DatabaseName)), 
                    StringComparer.OrdinalIgnoreCase);

                // Now, export the latest daily backups
                foreach (var dailyBackup in dailyBackups)
                {
                    new ExportDatabaseTask()
                    {
                        ConnectionString = ConnectionString,
                        DestinationStorage = DestinationStorage,
                        DestinationContainer = "database-backups",
                        SqlDacEndpoint = SqlDacEndpoint,
                        DatabaseName = dailyBackup.DatabaseName,
                        WhatIf = WhatIf
                    }.Execute();
                }

                if (keepOnline.Count == 0)
                {
                    throw new ApplicationException("Abort - sanity check failed - we are about to delete all backups");
                }

                foreach (var db in dbs)
                {
                    if (keepOnline.Contains(db.DatabaseName))
                    {
                        Log.Info("Retained backup: " + db.DatabaseName);
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

        private void DeleteDatabaseBackup(DatabaseBackup db, SqlExecutor dbExecutor)
        {
            if (!WhatIf)
            {
                dbExecutor.Execute(string.Format("DROP DATABASE {0}", db.DatabaseName));
            }
            Log.Info("Deleted database {0}.", db.DatabaseName);
        }
    }
}

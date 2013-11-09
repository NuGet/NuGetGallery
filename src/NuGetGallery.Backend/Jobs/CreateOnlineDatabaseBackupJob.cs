using System;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using NuGetGallery.Backend.Models;
using System.Globalization;

namespace NuGetGallery.Backend.Jobs
{
    public class CreateOnlineDatabaseBackupJob : Job
    {
        /// <summary>
        /// The target server, in the form of a known SQL Server (primary, warehouse, etc.)
        /// </summary>
        public KnownSqlServer TargetServer { get; set; }
        
        /// <summary>
        /// The name of the database to back up
        /// </summary>
        public string TargetDatabaseName { get; set; }

        /// <summary>
        /// A connection string to the database to be backed up. The user credentials must
        /// also be valid for connecting to the master database on that server.
        /// </summary>
        public SqlConnectionStringBuilder TargetDatabaseConnection { get; set; }

        /// <summary>
        /// The prefix to apply to the backup
        /// </summary>
        public string BackupPrefix { get; set; }

        /// <summary>
        /// The maximum age of the latest backup. If there isn't one younger than this value, a backup
        /// will be created. If there is one younger and Force is not specified, a backup
        /// will not be created.
        /// </summary>
        public TimeSpan? MaxAge { get; set; }

        /// <summary>
        /// Forces a new backup to be created
        /// </summary>
        public bool Force { get; set; }

        public override EventSource GetEventSource()
        {
            return JobEventSource.Log;
        }

        protected internal override async Task Execute()
        {
            // Resolve the connection if not specified explicitly
            if (TargetDatabaseConnection == null)
            {
                TargetDatabaseConnection = Config
                    .GetSqlServer(TargetServer)
                    .ChangeDatabase(TargetDatabaseName);
            }
            JobEventSource.Log.PreparingToBackup(
                TargetDatabaseConnection.InitialCatalog, 
                TargetDatabaseConnection.DataSource);
            // Connect to the master database
            using (var connection = await TargetDatabaseConnection.ConnectToMaster())
            {
                if (!Force && MaxAge != null)
                {
                    // Get databases
                    JobEventSource.Log.GettingDatabaseList(TargetDatabaseConnection.DataSource);
                    var databases = await GetDatabases(connection);

                    // Gather backups with matching prefix and order descending
                    var ordered = from db in databases
                                  let backupMeta = db.GetBackupMetadata()
                                  where backupMeta != null && 
                                        String.Equals(
                                            BackupPrefix, 
                                            backupMeta.Prefix, 
                                            StringComparison.OrdinalIgnoreCase)
                                  orderby backupMeta.Timestamp descending
                                  select backupMeta;

                    // Take the most recent one and check it's time
                    var mostRecent = ordered.FirstOrDefault();
                    if (mostRecent != null && mostRecent.Timestamp.IsYoungerThan(MaxAge.Value))
                    {
                        // Skip the backup
                        JobEventSource.Log.BackupWithinMaxAge(mostRecent, MaxAge.Value);
                        return;
                    }
                }

                // Generate a backup name
                string backupName = DatabaseBackup.GetName(BackupPrefix, DateTimeOffset.UtcNow);

                // Start a copy
                //  (have to build the SQL string manually because you can't parameterize CREATE DATABASE)
                JobEventSource.Log.StartingCopy(TargetDatabaseConnection.InitialCatalog, backupName);
                await connection.ExecuteAsync(String.Format(
                    CultureInfo.InvariantCulture,
                    "CREATE DATABASE {0} AS COPY OF {1}",
                    backupName,
                    TargetDatabaseConnection.InitialCatalog));
                JobEventSource.Log.StartedCopy(TargetDatabaseConnection.InitialCatalog, backupName);

                // Return a result to queue an async completion check in 5 minutes.
                return JobResult.AsyncCompletion(new
                {
                    DatabaseName = backupName
                }, TimeSpan.FromMinutes(5));
            }
        }

        protected internal virtual Task<IEnumerable<Database>> GetDatabases(SqlConnection connection)
        {
            return connection.QueryAsync<Database>(@"
                SELECT name, database_id, create_date, state
                FROM sys.databases
            ");
        }

        [EventSource(Name="NuGet-Jobs-CreateOnlineDatabaseBackup")]
        public class JobEventSource : EventSource
        {
            public static readonly JobEventSource Log = new JobEventSource();

            private JobEventSource() { }

#pragma warning disable 0618
            [Event(
                eventId: 1,
                Level = EventLevel.Informational,
                Message = "Skipping backup. {0} is within maximum age of {1}.")]
            [Obsolete("This method supports ETL infrastructure. Use other overloads instead")]
            public void BackupWithinMaxAge(string name, string age) { WriteEvent(1, name, age); }

            [NonEvent]
            public void BackupWithinMaxAge(DatabaseBackup mostRecent, TimeSpan timeSpan) { BackupWithinMaxAge(mostRecent.Db.name, timeSpan.ToString()); }

            [Event(
                eventId: 2,
                Level = EventLevel.Informational,
                Message = "Getting list of databases on {0}")]
            public void GettingDatabaseList(string server) { WriteEvent(2, server); }

            [Event(
                eventId: 3,
                Level = EventLevel.Informational,
                Message = "Preparing to backup {1} on server {0}")]
            public void PreparingToBackup(string server, string database) { WriteEvent(3, server, database); }

            [Event(
                eventId: 4,
                Level = EventLevel.Informational,
                Message = "Starting copy of {0} to {1}")]
            public void StartingCopy(string source, string destination) { WriteEvent(4, source, destination); }

            [Event(
                eventId: 5,
                Level = EventLevel.Informational,
                Message = "Started copy of {0} to {1}")]
            public void StartedCopy(string source, string destination) { WriteEvent(5, source, destination); }
#pragma warning restore 0618
        }
    }
}

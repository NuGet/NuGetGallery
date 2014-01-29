using System;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Globalization;
using NuGet.Services.Configuration;
using NuGet.Services.Work.Jobs.Models;
using System.ComponentModel;

namespace NuGet.Services.Work.Jobs
{
    [Description("Creates an Online backup of the Target SQL Database")]
    public class CreateOnlineDatabaseBackupJob : DatabaseJobHandlerBase<CreateOnlineDatabaseBackupEventSource>
    {
        public static readonly string DefaultBackupPrefix = "Backup";

        /// <summary>
        /// The prefix to apply to the backup
        /// </summary>
        public string BackupPrefix { get; set; }

        /// <summary>
        /// The name of the backup to create, uses BackupPrefix and current time to generate one if not specified
        /// </summary>
        public string BackupName { get; set; }

        /// <summary>
        /// The maximum age of the latest backup. If there isn't one younger than this value, a backup
        /// will be created. If there is one younger and Force is not specified, a backup
        /// will not be created.
        /// </summary>
        public TimeSpan? MaxAge { get; set; }

        public CreateOnlineDatabaseBackupJob(ConfigurationHub config) : base(config) { }

        protected internal override async Task<JobContinuation> Execute()
        {
            BackupPrefix = String.IsNullOrEmpty(BackupPrefix) ? DefaultBackupPrefix : BackupPrefix;

            // Resolve the connection if not specified explicitly
            var cstr = GetConnectionString(admin: true);
            Log.PreparingToBackup(
                cstr.DataSource,
                cstr.InitialCatalog);

            // Connect to the master database
            using (var connection = await cstr.ConnectToMaster())
            {
                if (MaxAge != null)
                {
                    // Get databases
                    Log.GettingDatabaseList(cstr.DataSource);
                    var databases = await GetDatabases(connection);

                    // Verify that the source database exists
                    if (!databases.Any(db => String.Equals(db.name, cstr.InitialCatalog, StringComparison.OrdinalIgnoreCase)))
                    {
                        Log.SourceDatabaseNotFound(cstr.InitialCatalog);
                        return Complete();
                    }

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
                    foreach (var backup in ordered)
                    {
                        Log.FoundBackup(backup);
                    }

                    // Take the most recent one and check it's time
                    var mostRecent = ordered.FirstOrDefault();
                    if (mostRecent != null && mostRecent.Timestamp.IsYoungerThan(MaxAge.Value))
                    {
                        // Skip the backup
                        Log.BackupWithinMaxAge(mostRecent, MaxAge.Value);
                        return Complete();
                    }
                }

                // Generate a backup name
                if (String.IsNullOrEmpty(BackupName))
                {
                    BackupName = DatabaseBackup.GetName(BackupPrefix, DateTimeOffset.UtcNow);
                }

                // Start a copy
                //  (have to build the SQL string manually because you can't parameterize CREATE DATABASE,
                //   but since the queue is secured there's no user input)
                Log.StartingCopy(cstr.InitialCatalog, BackupName);
                await connection.ExecuteAsync(String.Format(
                    CultureInfo.InvariantCulture,
                    "CREATE DATABASE [{0}] AS COPY OF [{1}]",
                    BackupName,
                    cstr.InitialCatalog));
                Log.StartedCopy(cstr.InitialCatalog, BackupName);

                // Check back in 5 minute intervals
                return ContinueCheckingBackup();
            }
        }

        protected internal override async Task<JobContinuation> Resume()
        {
            var cstr = GetConnectionString(admin: true);
            using (var connection = await cstr.ConnectToMaster())
            {
                var db = await GetDatabase(connection, BackupName);
                if (db == null)
                {
                    Log.CopyMissing(BackupName);
                    throw new Exception("Database copy was missing: " + BackupName);
                }

                switch (db.state)
                {
                    case DatabaseState.ONLINE:
                        Log.CopyComplete(cstr.InitialCatalog, BackupName);
                        return Complete();
                    case DatabaseState.SUSPECT:
                        Log.CopyFailed(cstr.InitialCatalog, BackupName);
                        throw new Exception("Database copy failed!");
                    case DatabaseState.COPYING:
                        Log.CopyContinuing(cstr.InitialCatalog, BackupName);
                        return ContinueCheckingBackup();
                    default:
                        Log.CopyStateUnexpected(cstr.InitialCatalog, BackupName, db.state);
                        throw new Exception("Database entered unexpected state: " + db.state.ToString());
                }
            }
        }

        private JobContinuation ContinueCheckingBackup()
        {
            var parameters = new Dictionary<string, string>() {
                { "BackupName", BackupName }
            };
            if (!String.IsNullOrEmpty(BackupPrefix))
            {
                parameters["BackupPrefix"] = BackupPrefix;
            }
            if (TargetDatabaseConnection != null)
            {
                parameters["TargetDatabaseConnection"] = TargetDatabaseConnection.ConnectionString;
            }
            else
            {
                parameters["TargetServer"] = TargetServer.ToString();
                parameters["TargetDatabaseName"] = TargetDatabaseName.ToString();
            }
            return Suspend(TimeSpan.FromMinutes(5), parameters);
        }
    }

    [EventSource(Name="Outercurve-NuGet-Jobs-CreateOnlineDatabaseBackup")]
    public class CreateOnlineDatabaseBackupEventSource : EventSource
    {
        public static readonly CreateOnlineDatabaseBackupEventSource Log = new CreateOnlineDatabaseBackupEventSource();

        private CreateOnlineDatabaseBackupEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Message = "Skipping backup. {0} is within maximum age of {1}.")]
        private void BackupWithinMaxAge(string name, string age) { WriteEvent(1, name, age); }

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
            Task = Tasks.Copy,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Started copy of {0} to {1}")]
        public void StartedCopy(string source, string destination) { WriteEvent(5, source, destination); }

        [Event(
            eventId: 6,
            Level = EventLevel.Critical,
            Task = Tasks.Copy,
            Opcode = EventOpcode.Stop,
            Message = "Backup database {0} is not present!")]
        public void CopyMissing(string dbName) { WriteEvent(6, dbName); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Task = Tasks.Copy,
            Opcode = EventOpcode.Stop,
            Message = "Copy of {0} to {1} complete!")]
        public void CopyComplete(string source, string destination) { WriteEvent(7, source, destination); }

        [Event(
            eventId: 8,
            Level = EventLevel.Informational,
            Task = Tasks.Copy,
            Message = "Copy of {0} to {1} still underway...")]
        public void CopyContinuing(string source, string destination) { WriteEvent(8, source, destination); }

        [Event(
            eventId: 9,
            Level = EventLevel.Error,
            Task = Tasks.Copy,
            Opcode = EventOpcode.Stop,
            Message = "Copy of {0} to {1} failed!")]
        public void CopyFailed(string source, string destination) { WriteEvent(9, source, destination); }

        [Event(
            eventId: 10,
            Level = EventLevel.Error,
            Task = Tasks.Copy,
            Opcode = EventOpcode.Stop,
            Message = "Copy of {0} to {1} entered unexpected state: {2} ({3})")]
        private void CopyStateUnexpected(string source, string destination, string stateName, int state) { WriteEvent(10, source, destination, stateName, state); }

        [NonEvent]
        public void CopyStateUnexpected(string source, string destination, DatabaseState state) { CopyStateUnexpected(source, destination, state.ToString(), (int)state); }

        [Event(
            eventId: 11,
            Level = EventLevel.Warning,
            Message = "Source database {0} not found!")]
        public void SourceDatabaseNotFound(string source) { WriteEvent(11, source); }

        [Event(
            eventId: 12,
            Level = EventLevel.Informational,
            Message = "Queued clean invocation {0}.")]
        private void QueuedClean(string id) { WriteEvent(12, id); }
        [NonEvent]
        public void QueuedClean(Guid id) { QueuedClean(id.ToString("N").ToLowerInvariant()); }

        [Event(
            eventId: 13,
            Level = EventLevel.Verbose,
            Message = "Found Backup {0} from {1}")]
        private void FoundBackup(string databaseName, string timestamp) { WriteEvent(13, databaseName, timestamp); }
        [NonEvent]
        public void FoundBackup(DatabaseBackup backup) { FoundBackup(backup.Db.name, backup.Timestamp.ToString()); }

        public class Tasks
        {
            public const EventTask Copy = (EventTask)0x1;
        }
    }
}

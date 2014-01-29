using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using NuGet.Services.Configuration;
using NuGet.Services.Work.Jobs.Models;

namespace NuGet.Services.Work.Jobs
{
    [Description("Cleans online database backups based on a provided policy")]
    public class CleanOnlineDatabaseBackupsJob : DatabaseJobHandlerBase<CleanOnlineDatabaseBackupsEventSource>
    {
        /// <summary>
        /// The prefix to apply to the backup
        /// </summary>
        public string BackupPrefix { get; set; }

        /// <summary>
        /// The maximum number of running backups to keep
        /// </summary>
        public int? MaxRunningBackups { get; set; }

        /// <summary>
        /// The maximum number of daily backups to keep (includes "today", so to keep today's last backup and yesterday's, specify 2)
        /// </summary>
        public int? MaxDailyBackups { get; set; }

        public CleanOnlineDatabaseBackupsJob(ConfigurationHub config) : base(config) { }

        protected internal override async Task<JobContinuation> Execute()
        {
            // Capture the current time in case the date changes during invocation
            DateTimeOffset now = DateTimeOffset.UtcNow;
            
            BackupPrefix = String.IsNullOrEmpty(BackupPrefix) ? CreateOnlineDatabaseBackupJob.DefaultBackupPrefix : BackupPrefix;

            // Resolve the connection if not specified explicitly
            var cstr = GetConnectionString(admin: true);
            Log.PreparingToClean(cstr.DataSource);

            // Connect to the master database
            using (var connection = await cstr.ConnectToMaster())
            {
                // Get online databases
                Log.GettingDatabaseList(cstr.DataSource);
                var backups = (await GetDatabases(connection, DatabaseState.ONLINE))
                    .Select(d => d.GetBackupMetadata())
                    .Where(b => b != null && String.Equals(BackupPrefix, b.Prefix))
                    .ToList();
                Log.GotDatabases(backups.Count, cstr.DataSource);

                // Start collecting a list of backups we're going to keep
                HashSet<DatabaseBackup> keepers = new HashSet<DatabaseBackup>();

                // Group backups by UTC Day
                var backupsByDate = backups
                    .GroupBy(b => b.Timestamp.UtcDateTime.Date)
                    .OrderByDescending(g => g.Key);

                // Keep the last backup from today and the max daily backups if any
                var dailyBackups = backupsByDate
                    .Take(MaxDailyBackups ?? 1)
                    .Select(g => g.OrderBy(db => db.Timestamp).Last());
                foreach (var keeper in dailyBackups)
                {
                    keepers.Add(keeper);
                }

                // Keep the most recent backups based on MaxRunningBackups
                foreach (var keeper in backups.OrderByDescending(b => b.Timestamp).Take(MaxRunningBackups ?? 1))
                {
                    keepers.Add(keeper);
                }

                // Report keepers
                foreach (var keeper in keepers)
                {
                    Log.KeepingBackup(keeper.Db.name);
                }

                // Delete the others!
                foreach (var db in backups.Except(keepers))
                {
                    Log.DeletingBackup(db.Db.name);
                    if (!WhatIf)
                    {
                        await connection.ExecuteAsync("DROP DATABASE [" + db.Db.name + "]");
                    }
                    Log.DeletedBackup(db.Db.name);
                }
            }
            return Complete();
        }
    }

    [EventSource(Name="Outercurve-NuGet-Jobs-CleanOnlineDatabaseBackups")]
    public class CleanOnlineDatabaseBackupsEventSource : EventSource
    {
        public static readonly CleanOnlineDatabaseBackupsEventSource Log = new CleanOnlineDatabaseBackupsEventSource();
        
        private CleanOnlineDatabaseBackupsEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Message = "Preparing to clean backups on {0}")]
        public void PreparingToClean(string server) { WriteEvent(1, server); }

        [Event(
            eventId: 2,
            Task = Tasks.GetDatabases,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Getting list of databases on {0}")]
        public void GettingDatabaseList(string server) { WriteEvent(2, server); }

        [Event(
            eventId: 3,
            Task = Tasks.GetDatabases,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Retrieved {0} ONLINE databases on {1}")]
        public void GotDatabases(int count, string server) { WriteEvent(3, count, server); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Message = "Keeping database: {0}")]
        public void KeepingBackup(string database) { WriteEvent(4, database); }

        [Event(
            eventId: 5,
            Task = Tasks.DeleteDatabase,
            Opcode = EventOpcode.Start,
            Level = EventLevel.Informational,
            Message = "Deleting database: {0}")]
        public void DeletingBackup(string database) { WriteEvent(5, database); }

        [Event(
            eventId: 6,
            Task = Tasks.DeleteDatabase,
            Opcode = EventOpcode.Stop,
            Level = EventLevel.Informational,
            Message = "Deleted database: {0}")]
        public void DeletedBackup(string database) { WriteEvent(6, database); }

        public class Tasks
        {
            public const EventTask GetDatabases = (EventTask)0x1;
            public const EventTask DeleteDatabase = (EventTask)0x2;
        }
    }
}

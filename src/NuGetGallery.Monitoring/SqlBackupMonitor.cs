using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;

namespace NuGetGallery.Monitoring
{
    public class SqlBackupMonitor : SqlMonitorBase
    {
        public static readonly int DefaultMaximumBackups = 5;
        public static readonly int DefaultMinimumBackups = 3;
        public static readonly string DefaultBackupPrefix = "Backup_";
        public static readonly TimeSpan DefaultBackupMinAge = TimeSpan.FromHours(1);

        public int MaximumBackups { get; set; }
        public int MinimumBackups { get; set; }
        public string BackupPrefix { get; set; }
        public TimeSpan BackupMinAge { get; set; }

        public SqlBackupMonitor(string server, string user, string password)
            : base(server, user, password)
        {
            MaximumBackups = DefaultMaximumBackups;
            MinimumBackups = DefaultMinimumBackups;
            BackupMinAge = DefaultBackupMinAge;
            BackupPrefix = DefaultBackupPrefix;
        }

        public SqlBackupMonitor(SqlConnectionStringBuilder connectionString)
            : base(connectionString)
        {
            MaximumBackups = DefaultMaximumBackups;
            MinimumBackups = DefaultMinimumBackups;
            BackupMinAge = DefaultBackupMinAge;
            BackupPrefix = DefaultBackupPrefix;
        }

        public SqlBackupMonitor(string connectionString)
            : base(new SqlConnectionStringBuilder(connectionString))
        {
            MaximumBackups = DefaultMaximumBackups;
            MinimumBackups = DefaultMinimumBackups;
            BackupMinAge = DefaultBackupMinAge;
            BackupPrefix = DefaultBackupPrefix;
        }

        protected override Task Invoke()
        {
            return Connect(c =>
            {
                // Get the databases matching the prefix
                var backups = c.Query(
                    "SELECT name, state FROM sys.databases WHERE name LIKE @prefix + '%'",
                    new { prefix = BackupPrefix })
                    .Select(d => new { Name = d.name, Timestamp = ParseTimestamp(d.name) })
                    .ToList();

                // First, check count
                if (backups.Count < MinimumBackups)
                {
                    Failure(String.Format("{0} backups found. Not enough backups!", backups.Count));
                }
                else if (backups.Count > MaximumBackups)
                {
                    Degraded(String.Format("{0} backups found. Too many backups!", backups.Count));
                }
                else
                {
                    // Find the youngest backup
                    var youngest = backups.OrderByDescending(b => b.Timestamp).FirstOrDefault();
                    if (youngest == null)
                    {
                        Failure("No timestamped backups!");
                    }
                    else if ((DateTime.UtcNow - youngest.Timestamp) > BackupMinAge)
                    {
                        Failure(String.Format(
                            "Backups are too old! Youngest is from {0}.",
                            youngest.Timestamp));
                    }
                    else
                    {
                        Success(String.Format("{0} backups. Youngest is from {1}",
                            backups.Count,
                            youngest.Timestamp));
                    }
                }
            });
        }

        protected override string FormatResourceName()
        {
            return base.FormatResourceName() + ";BackupPrefix=" + BackupPrefix;
        }

        // Copied because NuGetGallery.Operations targets .NET 4.5 but our monitors have to run in .NET 4.0 :(
        private static readonly Regex OldBackupNameFormat = new Regex(@"^(?<name>.+)_(?<timestamp>\d{14})$");
        private static readonly Regex BackupNameFormat = new Regex(@"^(?<name>.+)_(?<timestamp>\d{4}[A-Za-z]{3}\d{2}_\d{4})Z$"); // Backup_2013Apr12_1452Z
        public static DateTimeOffset? ParseTimestamp(string databaseName)
        {
            var match = BackupNameFormat.Match(databaseName);
            if (match.Success)
            {
                return ParseNewTimestamp(match.Groups["timestamp"].Value);
            }
            match = OldBackupNameFormat.Match(databaseName);
            if (match.Success)
            {
                return ParseOldTimestamp(match.Groups["timestamp"].Value);
            }
            return null;
        }

        private static DateTimeOffset ParseOldTimestamp(string timestamp)
        {
            return new DateTimeOffset(
                DateTime.ParseExact(timestamp, "yyyyMMddHHmmss", CultureInfo.CurrentCulture),
                TimeSpan.Zero);
        }

        private static DateTimeOffset ParseNewTimestamp(string timestamp)
        {
            return new DateTimeOffset(
                DateTime.ParseExact(timestamp, "yyyyMMMdd_HHmm", CultureInfo.CurrentCulture),
                TimeSpan.Zero);
        }
    }
}

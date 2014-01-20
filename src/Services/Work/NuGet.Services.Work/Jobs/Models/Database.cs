using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Jobs.Models
{
    public class Database
    {
        public string name { get; set; }
        public int database_id { get; set; }
        public DateTime create_date { get; set; }
        public DatabaseState state { get; set; }

        public DatabaseBackup GetBackupMetadata()
        {
            return DatabaseBackup.Create(this);
        }
    }

    public class DatabaseBackup {
        private const string BackupTimestampFormat = "yyyyMMMdd_HHmm";
        private static readonly Regex BackupNameParser = new Regex(@"(?<prefix>[^_]*)_(?<timestamp>\d{4}[A-Z]{3}\d{2}_\d{4})Z", RegexOptions.IgnoreCase);
        private const string BackupNameFormat = "{0}_{1:" + BackupTimestampFormat + "}Z";

        public Database Db { get; private set; }
        public string Prefix { get; private set; }
        public DateTimeOffset Timestamp { get; private set; }

        public DatabaseBackup(Database db, string prefix, DateTimeOffset timestamp)
        {
            Db = db;
            Prefix = prefix;
            Timestamp = timestamp;
        }

        internal static DatabaseBackup Create(Database db)
        {
            var match = BackupNameParser.Match(db.name);
            if (match.Success)
            {
                DateTimeOffset timestamp = DateTimeOffset.ParseExact(
                    match.Groups["timestamp"].Value,
                    BackupTimestampFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal);
                return new DatabaseBackup(db, match.Groups["prefix"].Value, timestamp);
            }
            return null;
        }

        public static string GetName(string prefix, DateTimeOffset timestamp)
        {
            return String.Format(BackupNameFormat, prefix, timestamp);
        }

        public override bool Equals(object obj)
        {
            DatabaseBackup other = obj as DatabaseBackup;
            return other != null && 
                String.Equals(other.Db.name, Db.name, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return Db.name.GetHashCode();
        }
    }

    public enum DatabaseState : byte
    {
        ONLINE = 0,
        RESTORING = 1,
        RECOVERING = 2,
        RECOVERY_PENDING = 3,
        SUSPECT = 4,
        EMERGENCY = 5,
        OFFLINE = 6,
        COPYING = 7
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Model
{
    public class DatabaseBackup
    {
        public string ServerName { get; private set; }
        public string DatabaseName { get; private set; }
        public DateTimeOffset? Timestamp { get; private set; }

        public DatabaseBackup(string serverName, string databaseName)
            : this(serverName, databaseName, ParseTimestamp(databaseName))
        {
        }

        public DatabaseBackup(string serverName, string databaseName, DateTimeOffset? timestamp)
        {
            ServerName = serverName;
            DatabaseName = databaseName;
            Timestamp = timestamp;
        }


        private static readonly Regex OldBackupNameFormat = new Regex(@"^(?<name>.+)_(?<timestamp>\d{14})$");
        private static readonly Regex BackupNameFormat = new Regex(@"^(?<name>.+)_(?<timestamp>\d{4}[A-Za-z]{3}\d{2}_\d{4})_UTC$"); // Backup_2013Apr12_1452_UTC
        private static DateTimeOffset? ParseTimestamp(string databaseName)
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
                DateTime.ParseExact(timestamp, "yyyyMMddHHmmss", CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal));
        }

        private static DateTimeOffset ParseNewTimestamp(string timestamp)
        {
            return new DateTimeOffset(
                DateTime.ParseExact(timestamp, "yyyyMMMdd_HHmm", CultureInfo.CurrentCulture, DateTimeStyles.AssumeUniversal));
        }
    }
}

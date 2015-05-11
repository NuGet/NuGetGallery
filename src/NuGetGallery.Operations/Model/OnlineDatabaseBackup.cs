// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Model
{
    public class OnlineDatabaseBackup
    {
        public int State { get; private set; }
        public string ServerName { get; private set; }
        public string DatabaseName { get; private set; }
        public DateTimeOffset? Timestamp { get; private set; }

        public OnlineDatabaseBackup(string serverName, string databaseName, int state)
            : this(serverName, databaseName, state, ParseTimestamp(databaseName))
        {
        }

        public OnlineDatabaseBackup(string serverName, string databaseName, int state, DateTimeOffset? timestamp)
        {
            State = state;
            Timestamp = timestamp;
            ServerName = serverName;
            DatabaseName = databaseName;
        }


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

        public override string ToString()
        {
            return ServerName + "." + DatabaseName;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }
}

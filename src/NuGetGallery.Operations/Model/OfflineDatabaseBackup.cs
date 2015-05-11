// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGetGallery.Operations.Model
{
    public class OfflineDatabaseBackup
    {
        private static readonly Regex OfflineBlobParser = new Regex(@"Backup_(?<timestamp>\d{4}[A-Za-z]{3}\d{2}_\d{4})Z\.bacpac");

        public CloudBlockBlob Blob { get; private set; }
        public DateTime Timestamp { get; private set; }

        public OfflineDatabaseBackup(CloudBlockBlob blob)
        {
            Blob = blob;

            var match = OfflineBlobParser.Match(blob.Name);
            if (!match.Success)
            {
                throw new FormatException("Invalid database backup name: " + blob.Name);
            }

            Timestamp = DateTime.ParseExact(match.Groups["timestamp"].Value, "yyyyMMMdd_HHmm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        public static bool IsOfflineBackup(CloudBlockBlob blob)
        {
            return OfflineBlobParser.IsMatch(blob.Name);
        }
    }
}

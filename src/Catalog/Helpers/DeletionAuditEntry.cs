// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Packaging.Core;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Helpers
{
    /// <summary>
    /// Represents the information in the audit entry for a deletion.
    /// </summary>
    public class DeletionAuditEntry
    {
        /// <summary>
        /// Over time, we have had multiple file names for audit records for deletes.
        /// </summary>
        public readonly static IList<string> FileNameSuffixes = new List<string>
        {
            "-Deleted.audit.v1.json",
            "-deleted.audit.v1.json",
            "-softdeleted.audit.v1.json",
            "-softdelete.audit.v1.json",
            "-delete.audit.v1.json"
        };

        /// <summary>
        /// Creates a <see cref="DeletionAuditEntry"/> from a <see cref="System.Uri"/> and a <see cref="Storage"/>.
        /// </summary>
        /// <param name="auditingStorage"><see cref="Storage"/> through which <paramref name="uri"/> can be accessed.</param>
        /// <param name="uri"><see cref="System.Uri"/> to the record to build a <see cref="DeletionAuditEntry"/> from.</param>
        public static async Task<DeletionAuditEntry> Create(Storage auditingStorage, Uri uri, CancellationToken cancellationToken, ILogger logger)
        {
            try
            {
                return new DeletionAuditEntry(uri, await auditingStorage.LoadString(uri, cancellationToken));
            }
            catch (JsonReaderException)
            {
                logger.LogWarning("Audit record at {AuditRecordUri} contains invalid JSON.", uri);
            }
            catch (NullReferenceException)
            {
                logger.LogWarning("Audit record at {AuditRecordUri} does not contain required JSON properties to perform a package delete.", uri);
            }
            catch (ArgumentException)
            {
                logger.LogWarning("Audit record at {AuditRecordUri} has no contents.", uri);
            }

            return null;
        }

        private DeletionAuditEntry(Uri uri, string contents)
        {
            if (string.IsNullOrEmpty(contents))
            {
                throw new ArgumentException($"{nameof(contents)} must not be null or empty!", nameof(contents));
            }

            Uri = uri;
            Record = JObject.Parse(contents);
            InitValues();
        }

        /// <summary>
        /// The <see cref="Uri"/> for the audit entry.
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// The entire contents of the audit entry file.
        /// </summary>
        public JObject Record { get; set; }

        /// <summary>
        /// The id of the package being audited.
        /// </summary>
        public string PackageId { get; set; }

        /// <summary>
        /// The version of the package being audited.
        /// </summary>
        public string PackageVersion { get; set; }

        /// <summary>
        /// The <see cref="DateTime"/> the package was deleted.
        /// </summary>
        public DateTime? TimestampUtc { get; set; }

        private const string RecordPart = "Record";
        private const string ActorPart = "Actor";

        private JObject GetPart(string partName)
        {
            return (JObject)Record?.GetValue(partName, StringComparison.OrdinalIgnoreCase);
        }

        private void InitValues()
        {
            PackageId = GetPart(RecordPart).GetValue("Id", StringComparison.OrdinalIgnoreCase).ToString();
            PackageVersion = GetPart(RecordPart).GetValue("Version", StringComparison.OrdinalIgnoreCase).ToString();
            TimestampUtc =
                GetPart(ActorPart).GetValue("TimestampUtc", StringComparison.OrdinalIgnoreCase).Value<DateTime>();
        }

        /// <summary>
        /// Fetches <see cref="DeletionAuditEntry"/>s.
        /// </summary>
        /// <param name="auditingStorage">The <see cref="Storage"/> to fetch audit records from.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the task.</param>
        /// <param name="package">If specified, will only fetch <see cref="DeletionAuditEntry"/>s for this package.</param>
        /// <param name="minTime">If specified, will only fetch <see cref="DeletionAuditEntry"/>s that are newer than this <see cref="DateTime"/> (non-inclusive).</param>
        /// <param name="maxTime">If specified, will only fetch <see cref="DeletionAuditEntry"/>s that are older than this <see cref="DateTime"/> (non-inclusive).</param>
        /// <param name="logger">An <see cref="ILogger"/> to log messages to.</param>
        /// <returns>An <see cref="IEnumerable{DeletionAuditEntry}"/> containing the relevant <see cref="DeletionAuditEntry"/>s.</returns>
        public static async Task<IEnumerable<DeletionAuditEntry>> Get(Storage auditingStorage, CancellationToken cancellationToken, PackageIdentity package = null, DateTime? minTime = null, DateTime? maxTime = null, ILogger logger = null)
        {
            Func<StorageListItem, bool> filterAuditRecord = (record) =>
            {
                if (!IsPackageDelete(record))
                {
                    return false;
                }

                if (package != null && GetAuditRecordPrefix(record.Uri) != $"package/{package.Id}/{package.Version}")
                {
                    return false;
                }

                // We can't do anything if the last modified time is not available.
                if (record.LastModifiedUtc == null)
                {
                    logger?.LogWarning("Could not get date for filename in filterAuditRecord. Uri: {AuditRecordUri}", record.Uri);
                    return false;
                }

                var recordTimestamp = record.LastModifiedUtc.Value;
                if (minTime != null && recordTimestamp < minTime.Value)
                {
                    return false;
                }

                if (maxTime != null && recordTimestamp > maxTime.Value)
                {
                    return false;
                }

                return true;
            };

            // Get all audit blobs (based on their filename which starts with a date that can be parsed).
            /// Filter on the <see cref="PackageIdentity"/> and <see cref="DateTime"/> fields provided.
            var auditRecords =
                (await auditingStorage.List(cancellationToken)).Where(filterAuditRecord);

            return
                (await Task.WhenAll(
                    auditRecords.Select(record => DeletionAuditEntry.Create(auditingStorage, record.Uri, cancellationToken, logger))))
                // Filter out null records.
                .Where(entry => entry?.Record != null);
        }

        /// <summary>
        /// Returns the prefix of the audit record, which contains the id and version of the package being audited.
        /// </summary>
        private static string GetAuditRecordPrefix(Uri uri)
        {
            var parts = uri.PathAndQuery.Split('/');
            return string.Join("/", parts.Where(p => !string.IsNullOrEmpty(p)).ToList().GetRange(0, parts.Length - 2).ToArray());
        }

        /// <summary>
        /// Returns the file name of the audit record, which contains the <see cref="DateTime"/> the record was made as well as the type of record it is.
        /// </summary>
        private static string GetAuditRecordFileName(Uri uri)
        {
            var parts = uri.PathAndQuery.Split('/');
            return parts.Length > 0 ? parts[parts.Length - 1] : null;
        }

        private static bool IsPackageDelete(StorageListItem auditRecord)
        {
            var fileName = GetAuditRecordFileName(auditRecord.Uri);
            return FileNameSuffixes.Any(suffix => fileName.EndsWith(suffix));
        }
    }
}

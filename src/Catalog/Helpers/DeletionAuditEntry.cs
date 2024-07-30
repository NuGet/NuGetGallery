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
        /// Asynchronously creates a <see cref="DeletionAuditEntry" /> from a <see cref="Uri" />
        /// and an <see cref="IStorage" />.
        /// </summary>
        /// <param name="auditingStorage">The <see cref="IStorage" /> through which <paramref name="uri"/>
        /// can be accessed.</param>
        /// <param name="uri">A <see cref="Uri" /> to the record to build a <see cref="DeletionAuditEntry" />
        /// from.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <param name="logger">A logger.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <see cref="DeletionAuditEntry" />
        /// instance.</returns>
        public static async Task<DeletionAuditEntry> CreateAsync(
            IStorage auditingStorage,
            Uri uri,
            CancellationToken cancellationToken,
            ILogger logger)
        {
            try
            {
                return new DeletionAuditEntry(uri, await auditingStorage.LoadStringAsync(uri, cancellationToken));
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

        [JsonConstructor]
        public DeletionAuditEntry()
        {
        }

        /// <summary>
        /// Constructor for testing.
        /// </summary>
        public DeletionAuditEntry(Uri uri, JObject record, string id, string version, DateTime? timestamp)
        {
            Uri = uri;
            Record = record;
            PackageId = id;
            PackageVersion = version;
            TimestampUtc = timestamp;
        }

        /// <summary>
        /// The <see cref="Uri"/> for the audit entry.
        /// </summary>
        [JsonProperty("uri")]
        public Uri Uri { get; private set; }

        /// <summary>
        /// The entire contents of the audit entry file.
        /// </summary>
        [JsonProperty("record")]
        public JObject Record { get; private set; }

        /// <summary>
        /// The id of the package being audited.
        /// </summary>
        [JsonProperty("id")]
        public string PackageId { get; private set; }

        /// <summary>
        /// The version of the package being audited.
        /// </summary>
        [JsonProperty("version")]
        public string PackageVersion { get; private set; }

        /// <summary>
        /// The <see cref="DateTime"/> the package was deleted.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime? TimestampUtc { get; private set; }

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
        /// <param name="auditingStorageFactory">The <see cref="StorageFactory"/> to fetch audit records from.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the task.</param>
        /// <param name="package">If specified, will only fetch <see cref="DeletionAuditEntry"/>s that represent operations on this package.</param>
        /// <param name="minTime">If specified, will only fetch <see cref="DeletionAuditEntry"/>s that are newer than this <see cref="DateTime"/> (non-inclusive).</param>
        /// <param name="maxTime">If specified, will only fetch <see cref="DeletionAuditEntry"/>s that are older than this <see cref="DateTime"/> (non-inclusive).</param>
        /// <param name="logger">An <see cref="ILogger"/> to log messages to.</param>
        /// <returns>An <see cref="IEnumerable{DeletionAuditEntry}"/> containing the relevant <see cref="DeletionAuditEntry"/>s.</returns>
        public static Task<IEnumerable<DeletionAuditEntry>> GetAsync(
            StorageFactory auditingStorageFactory,
            CancellationToken cancellationToken,
            PackageIdentity package = null,
            DateTime? minTime = null,
            DateTime? maxTime = null,
            ILogger logger = null)
        {
            Storage storage = auditingStorageFactory.Create(package != null ? GetAuditRecordPrefixFromPackage(package) : null);
            return GetAsync(storage, cancellationToken, minTime, maxTime, logger);
        }

        /// <summary>
        /// Asynchronously fetches <see cref="DeletionAuditEntry"/>s.
        /// </summary>
        /// <param name="auditingStorage">The <see cref="IStorage"/> to fetch audit records from.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel
        /// the task.</param>
        /// <param name="minTime">If specified, will only fetch <see cref="DeletionAuditEntry"/>s that are newer than
        /// this <see cref="DateTime"/> (non-inclusive).</param>
        /// <param name="maxTime">If specified, will only fetch <see cref="DeletionAuditEntry"/>s that are older than
        /// this <see cref="DateTime"/> (non-inclusive).</param>
        /// <param name="logger">An <see cref="ILogger"/> to log messages to.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns an
        /// <see cref="IEnumerable{DeletionAuditEntry}" /> containing the relevant
        /// <see cref="DeletionAuditEntry"/>s.</returns>
        public static async Task<IEnumerable<DeletionAuditEntry>> GetAsync(
            IStorage auditingStorage,
            CancellationToken cancellationToken,
            DateTime? minTime = null,
            DateTime? maxTime = null,
            ILogger logger = null)
        {
            Func<StorageListItem, bool> filterAuditRecord = (record) =>
            {
                if (!IsPackageDelete(record))
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
                (await auditingStorage.ListAsync(cancellationToken)).Where(filterAuditRecord);

            return
                (await Task.WhenAll(
                    auditRecords.Select(record => DeletionAuditEntry.CreateAsync(auditingStorage, record.Uri, cancellationToken, logger))))
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

        private static string GetAuditRecordPrefixFromPackage(PackageIdentity package)
        {
            return $"{package.Id.ToLowerInvariant()}/{package.Version.ToNormalizedString().ToLowerInvariant()}";
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
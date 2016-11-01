// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Metadata.Catalog;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using NuGet.Services.Configuration;

namespace Ng.Jobs
{
    public class Feed2CatalogJob : LoopingNgJob
    {
        private static readonly DateTime DateTimeMinValueUtc = new DateTime(0L, DateTimeKind.Utc);

        protected bool Verbose;
        protected string Gallery;
        protected Storage CatalogStorage;
        protected Storage AuditingStorage;
        protected DateTime? StartDate;
        protected TimeSpan Timeout;
        protected int Top;

        public Feed2CatalogJob(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public override string GetUsage()
        {
            return "Usage: ng feed2catalog "
                   + $"-{Arguments.Gallery} <v2-feed-address> "
                   + $"-{Arguments.StorageBaseAddress} <storage-base-address> "
                   + $"-{Arguments.StorageType} file|azure "
                   + $"[-{Arguments.StoragePath} <path>]"
                   + "|"
                   + $"[-{Arguments.StorageAccountName} <azure-acc> "
                   + $"-{Arguments.StorageKeyValue} <azure-key> "
                   + $"-{Arguments.StorageContainer} <azure-container> "
                   + $"-{Arguments.StoragePath} <path> "
                   + $"[-{Arguments.VaultName} <keyvault-name> "
                   + $"-{Arguments.ClientId} <keyvault-client-id> "
                   + $"-{Arguments.CertificateThumbprint} <keyvault-certificate-thumbprint> "
                   + $"[-{Arguments.ValidateCertificate} true|false]]] "
                   + $"-{Arguments.StorageTypeAuditing} file|azure "
                   + $"[-{Arguments.StoragePathAuditing} <path>]"
                   + "|"
                   + $"[-{Arguments.StorageAccountNameAuditing} <azure-acc> "
                   + $"-{Arguments.StorageKeyValueAuditing} <azure-key> "
                   + $"-{Arguments.StorageContainerAuditing} <azure-container> "
                   + $"-{Arguments.StoragePathAuditing} <path>] "
                   + $"[-{Arguments.Verbose} true|false] "
                   + $"[-{Arguments.Interval} <seconds>] "
                   + $"[-{Arguments.StartDate} <DateTime>]";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            Gallery = arguments.GetOrThrow<string>(Arguments.Gallery);
            Verbose = arguments.GetOrDefault(Arguments.Verbose, false);
            StartDate = arguments.GetOrDefault(Arguments.StartDate, DateTimeMinValueUtc);

            var catalogStorageFactory = CommandHelpers.CreateStorageFactory(arguments, Verbose);
            var auditingStorageFactory = CommandHelpers.CreateSuffixedStorageFactory("Auditing", arguments, Verbose);

            Logger.LogInformation("CONFIG source: \"{ConfigSource}\" storage: \"{Storage}\"", Gallery, catalogStorageFactory);

            CatalogStorage = catalogStorageFactory.Create();
            AuditingStorage = auditingStorageFactory.Create();

            Top = 20;
            Timeout = TimeSpan.FromSeconds(300);
        }

        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            using (var client = CreateHttpClient(Verbose))
            {
                client.Timeout = Timeout;

                // baseline timestamps
                var lastCreated = await CatalogUtility.GetCatalogProperty(CatalogStorage, "nuget:lastCreated", cancellationToken) ?? (StartDate ?? DateTimeMinValueUtc);
                var lastEdited = await CatalogUtility.GetCatalogProperty(CatalogStorage, "nuget:lastEdited", cancellationToken) ?? lastCreated;
                var lastDeleted = await CatalogUtility.GetCatalogProperty(CatalogStorage, "nuget:lastDeleted", cancellationToken) ?? lastCreated;
                if (lastDeleted == DateTime.MinValue.ToUniversalTime())
                {
                    lastDeleted = lastCreated;
                }

                // fetch and add all DELETED packages
                if (lastDeleted > DateTime.MinValue.ToUniversalTime())
                {
                    SortedList<DateTime, IList<CatalogUtility.PackageIdentity>> deletedPackages;
                    var previousLastDeleted = DateTime.MinValue;
                    do
                    {
                        // Get deleted packages
                        Logger.LogInformation("CATALOG LastDeleted: {CatalogDeletedTime}", lastDeleted.ToString("O"));

                        deletedPackages = await GetDeletedPackages(AuditingStorage, lastDeleted);

                        Logger.LogInformation("FEED DeletedPackages: {DeletedPackagesCount}", deletedPackages.Count);

                        // We want to ensure a commit only contains each package once at most.
                        // Therefore we segment by package id + version.
                        var deletedPackagesSegments = SegmentPackageDeletes(deletedPackages);
                        foreach (var deletedPackagesSegment in deletedPackagesSegments)
                        {
                            lastDeleted = await Deletes2Catalog(
                                deletedPackagesSegment, CatalogStorage, lastCreated, lastEdited, lastDeleted, cancellationToken);

                            // Wait for one second to ensure the next catalog commit gets a new timestamp
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                        }

                        if (previousLastDeleted == lastDeleted)
                        {
                            break;
                        }
                        previousLastDeleted = lastDeleted;
                    }
                    while (deletedPackages.Count > 0);
                }

                //  THEN fetch and add all newly CREATED packages - in order
                SortedList<DateTime, IList<CatalogUtility.PackageDetails>> createdPackages;
                var previousLastCreated = DateTime.MinValue;
                do
                {
                    Logger.LogInformation("CATALOG LastCreated: {CatalogLastCreatedTime}", lastCreated.ToString("O"));

                    createdPackages = await GetCreatedPackages(client, Gallery, lastCreated, Top);
                    Logger.LogInformation("FEED CreatedPackages: {CreatedPackagesCount}", createdPackages.Count);

                    lastCreated = await CatalogUtility.DownloadMetadata2Catalog(
                        client, createdPackages, CatalogStorage, lastCreated, lastEdited, lastDeleted, true, cancellationToken, Logger);
                    if (previousLastCreated == lastCreated)
                    {
                        break;
                    }
                    previousLastCreated = lastCreated;
                }
                while (createdPackages.Count > 0);

                //  THEN fetch and add all EDITED packages - in order
                SortedList<DateTime, IList<CatalogUtility.PackageDetails>> editedPackages;
                var previousLastEdited = DateTime.MinValue;
                do
                {
                    Logger.LogInformation("CATALOG LastEdited: {CatalogLastEditedTime}", lastEdited.ToString("O"));

                    editedPackages = await GetEditedPackages(client, Gallery, lastEdited, Top);

                    Logger.LogInformation("FEED EditedPackages: {EditedPackagesCount}", editedPackages.Count);

                    lastEdited = await CatalogUtility.DownloadMetadata2Catalog(
                        client, editedPackages, CatalogStorage, lastCreated, lastEdited, lastDeleted, false, cancellationToken, Logger);
                    if (previousLastEdited == lastEdited)
                    {
                        break;
                    }
                    previousLastEdited = lastEdited;
                }
                while (editedPackages.Count > 0);
            }
        }

        // Wrapper function for CatalogUtility.CreateHttpClient
        // Overriden by NgTests.TestableFeed2CatalogJob
        protected virtual HttpClient CreateHttpClient(bool verbose)
        {
            return CatalogUtility.CreateHttpClient(verbose);
        }

        private static Uri MakeCreatedUri(string source, DateTime since, int top = 100)
        {
            var address = string.Format("{0}/Packages?$filter=Created gt DateTime'{1}'&$top={2}&$orderby=Created&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl",
                source.Trim('/'),
                since.ToString("o"),
                top);

            return new Uri(address);
        }

        private static Uri MakeLastEditedUri(string source, DateTime since, int top = 100)
        {
            var address = string.Format("{0}/Packages?$filter=LastEdited gt DateTime'{1}'&$top={2}&$orderby=LastEdited&$select=Created,LastEdited,Published,LicenseNames,LicenseReportUrl",
                source.Trim('/'),
                since.ToString("o"),
                top);

            return new Uri(address);
        }

        private static Task<SortedList<DateTime, IList<CatalogUtility.PackageDetails>>> GetCreatedPackages(HttpClient client, string source, DateTime since, int top = 100)
        {
            return CatalogUtility.GetPackages(client, MakeCreatedUri(source, since, top), "Created");
        }

        private static Task<SortedList<DateTime, IList<CatalogUtility.PackageDetails>>> GetEditedPackages(HttpClient client, string source, DateTime since, int top = 100)
        {
            return CatalogUtility.GetPackages(client, MakeLastEditedUri(source, since, top), "LastEdited");
        }

        private async Task<SortedList<DateTime, IList<CatalogUtility.PackageIdentity>>> GetDeletedPackages(Storage auditingStorage, DateTime since)
        {
            var result = new SortedList<DateTime, IList<CatalogUtility.PackageIdentity>>();

            // Get all audit blobs (based on their filename which starts with a date that can be parsed)
            // NOTE we're getting more files than needed (to account for a time difference between servers)
            var minimumFileTime = since.AddMinutes(-15);
            var auditRecordUris = (await auditingStorage.List(CancellationToken.None))
                .Where(recordUri => FilterDeletedPackage(minimumFileTime, recordUri));

            foreach (var auditRecordUri in auditRecordUris)
            {
                var contents = await auditingStorage.LoadString(auditRecordUri, CancellationToken.None);
                if (!string.IsNullOrEmpty(contents))
                {
                    string packageId;
                    string packageVersion;
                    DateTime? deletedOn;
                    try
                    {
                        var auditRecord = JObject.Parse(contents);

                        var recordPart = (JObject)auditRecord.GetValue("Record", StringComparison.OrdinalIgnoreCase);
                        packageId = recordPart.GetValue("Id", StringComparison.OrdinalIgnoreCase).ToString();
                        packageVersion = recordPart.GetValue("Version", StringComparison.OrdinalIgnoreCase).ToString();

                        var actorPart = (JObject)auditRecord.GetValue("Actor", StringComparison.OrdinalIgnoreCase);
                        deletedOn = actorPart.GetValue("TimestampUtc", StringComparison.OrdinalIgnoreCase).Value<DateTime>();
                    }
                    catch (JsonReaderException)
                    {
                        Logger.LogWarning("Audit record at {AuditRecordUri} contains invalid JSON.", auditRecordUri);
                        continue;
                    }
                    catch (NullReferenceException)
                    {
                        Logger.LogWarning("Audit record at {AuditRecordUri} does not contain required JSON properties to perform a package delete.", auditRecordUri);
                        continue;
                    }

                    if (!string.IsNullOrEmpty(packageId) && !string.IsNullOrEmpty(packageVersion) && deletedOn > since)
                    {
                        // Mark the package "deleted"
                        IList<CatalogUtility.PackageIdentity> packages;
                        if (!result.TryGetValue(deletedOn.Value, out packages))
                        {
                            packages = new List<CatalogUtility.PackageIdentity>();
                            result.Add(deletedOn.Value, packages);
                        }

                        packages.Add(new CatalogUtility.PackageIdentity(packageId, packageVersion));
                    }
                }
            }

            return result;
        }

        private bool FilterDeletedPackage(DateTime minimumFileTime, Uri auditRecordUri)
        {
            var fileName = GetFileName(auditRecordUri);

            // over time, we have had three "deleted" file names. Try working with them all.
            if (fileName.EndsWith("-Deleted.audit.v1.json") || fileName.EndsWith("-deleted.audit.v1.json") || fileName.EndsWith("-softdeleted.audit.v1.json"))
            {
                var deletedDateTimeString = fileName
                    .Replace("-Deleted.audit.v1.json", string.Empty)
                    .Replace("-deleted.audit.v1.json", string.Empty)
                    .Replace("-softdeleted.audit.v1.json", string.Empty)
                    .Replace("_", ":");

                DateTime recordTime;
                if (DateTime.TryParse(deletedDateTimeString, out recordTime))
                {
                    return recordTime >= minimumFileTime;
                }

                Logger.LogWarning("Could not parse date from filename in FilterDeletedPackage. Uri: {AuditRecordUri}", auditRecordUri);
            }

            return false;
        }

        private static string GetFileName(Uri uri)
        {
            var parts = uri.PathAndQuery.Split('/');

            if (parts.Length > 0)
            {
                return parts[parts.Length - 1];
            }

            return null;
        }

        private static IEnumerable<SortedList<DateTime, IList<CatalogUtility.PackageIdentity>>> SegmentPackageDeletes(SortedList<DateTime, IList<CatalogUtility.PackageIdentity>> packageDeletes)
        {
            var packageIdentityTracker = new HashSet<string>();
            var currentSegment = new SortedList<DateTime, IList<CatalogUtility.PackageIdentity>>();
            foreach (var entry in packageDeletes)
            {
                if (!currentSegment.ContainsKey(entry.Key))
                {
                    currentSegment.Add(entry.Key, new List<CatalogUtility.PackageIdentity>());
                }

                var curentSegmentPackages = currentSegment[entry.Key];
                foreach (var packageIdentity in entry.Value)
                {
                    var key = packageIdentity.Id + "|" + packageIdentity.Version;
                    if (packageIdentityTracker.Contains(key))
                    {
                        // Duplicate, return segment
                        yield return currentSegment;

                        // Clear current segment
                        currentSegment.Clear();
                        currentSegment.Add(entry.Key, new List<CatalogUtility.PackageIdentity>());
                        curentSegmentPackages = currentSegment[entry.Key];
                        packageIdentityTracker.Clear();
                    }

                    // Add to segment
                    curentSegmentPackages.Add(packageIdentity);
                    packageIdentityTracker.Add(key);
                }
            }

            if (currentSegment.Any())
            {
                yield return currentSegment;
            }
        }

        private async Task<DateTime> Deletes2Catalog(SortedList<DateTime, IList<CatalogUtility.PackageIdentity>> packages, Storage storage, DateTime lastCreated, DateTime lastEdited, DateTime lastDeleted, CancellationToken cancellationToken)
        {
            var writer = new AppendOnlyCatalogWriter(storage, maxPageSize: 550);

            if (packages == null || packages.Count == 0)
            {
                return lastDeleted;
            }

            foreach (var entry in packages)
            {
                foreach (var packageIdentity in entry.Value)
                {
                    var catalogItem = new DeleteCatalogItem(packageIdentity.Id, packageIdentity.Version, entry.Key);
                    writer.Add(catalogItem);

                    Logger.LogInformation("Delete: {PackageId} {PackageVersion}", packageIdentity.Id, packageIdentity.Version);
                }

                lastDeleted = entry.Key;
            }

            var commitMetadata = PackageCatalog.CreateCommitMetadata(writer.RootUri, new CommitMetadata(lastCreated, lastEdited, lastDeleted));

            await writer.Commit(commitMetadata, cancellationToken);

            Logger.LogInformation("COMMIT package deletes to catalog.");

            return lastDeleted;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Catalog;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    public class DnxCatalogCollector : CommitCollector
    {
        private readonly StorageFactory _storageFactory;
        private readonly IAzureStorage _sourceStorage;
        private readonly DnxMaker _dnxMaker;
        private readonly Func<HttpClient, ICatalogClient> _catalogClientFactory;
        private readonly ILogger _logger;
        private readonly int _maxConcurrentBatches;
        private readonly int _maxConcurrentCommitItemsWithinBatch;
        private readonly Uri _contentBaseAddress;

        public DnxCatalogCollector(
            Uri index,
            StorageFactory storageFactory,
            IAzureStorage preferredPackageSourceStorage,
            Uri contentBaseAddress,
            ITelemetryService telemetryService,
            ILogger logger,
            int maxDegreeOfParallelism,
            Func<HttpClient, ICatalogClient> catalogClientFactory,
            Func<HttpMessageHandler> handlerFunc = null,
            TimeSpan? httpClientTimeout = null)
            : base(index, telemetryService, handlerFunc, httpClientTimeout)
        {
            _storageFactory = storageFactory ?? throw new ArgumentNullException(nameof(storageFactory));
            _sourceStorage = preferredPackageSourceStorage;
            _contentBaseAddress = contentBaseAddress;
            _dnxMaker = new DnxMaker(storageFactory, telemetryService, logger);
            _catalogClientFactory = catalogClientFactory ?? throw new ArgumentNullException(nameof(catalogClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (maxDegreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDegreeOfParallelism),
                    string.Format(Strings.ArgumentOutOfRange, 1, int.MaxValue));
            }

            // Find two factors which are close or equal to each other.
            var squareRoot = Math.Sqrt(maxDegreeOfParallelism);

            // If the max degree of parallelism is a perfect square, great.
            // Otherwise, prefer a greater degree of parallelism in batches than commit items within a batch.
            _maxConcurrentBatches = Convert.ToInt32(Math.Ceiling(squareRoot));
            _maxConcurrentCommitItemsWithinBatch = Convert.ToInt32(maxDegreeOfParallelism / _maxConcurrentBatches);

            ServicePointManager.DefaultConnectionLimit = _maxConcurrentBatches * _maxConcurrentCommitItemsWithinBatch;
        }

        protected override Task<IEnumerable<CatalogCommitItemBatch>> CreateBatchesAsync(
            IEnumerable<CatalogCommitItem> catalogItems)
        {
            var batches = CatalogCommitUtilities.CreateCommitItemBatches(
                catalogItems,
                CatalogCommitUtilities.GetPackageIdKey);

            return Task.FromResult(batches);
        }

        protected override Task<bool> FetchAsync(
            CollectorHttpClient client,
            ReadWriteCursor front,
            ReadCursor back,
            CancellationToken cancellationToken)
        {
            return CatalogCommitUtilities.ProcessCatalogCommitsAsync(
                client,
                front,
                back,
                FetchCatalogCommitsAsync,
                CreateBatchesAsync,
                ProcessBatchAsync,
                _maxConcurrentBatches,
                _logger,
                cancellationToken);
        }

        protected override async Task<bool> OnProcessBatchAsync(
            CollectorHttpClient client,
            IEnumerable<CatalogCommitItem> items,
            JToken context,
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken)
        {
            var catalogEntries = items.Select(item => CatalogEntry.Create(item))
                .ToList();

            // Sanity check:  a single catalog batch should not contain multiple entries for the same package identity.
            AssertNoMultipleEntriesForSamePackageIdentity(commitTimeStamp, catalogEntries);

            // Process .nupkg/.nuspec adds and deletes.
            var processedCatalogEntries = await ProcessCatalogEntriesAsync(client, catalogEntries, cancellationToken);

            // Update the package version index with adds and deletes.
            await UpdatePackageVersionIndexAsync(processedCatalogEntries, cancellationToken);

            return true;
        }

        private async Task<IEnumerable<CatalogEntry>> ProcessCatalogEntriesAsync(
            CollectorHttpClient client,
            IEnumerable<CatalogEntry> catalogEntries,
            CancellationToken cancellationToken)
        {
            var processedCatalogEntries = new ConcurrentBag<CatalogEntry>();

            await catalogEntries.ForEachAsync(_maxConcurrentCommitItemsWithinBatch, async catalogEntry =>
            {
                var packageId = catalogEntry.PackageId;
                var normalizedPackageVersion = catalogEntry.NormalizedPackageVersion;

                if (catalogEntry.Type.AbsoluteUri == Schema.DataTypes.PackageDetails.AbsoluteUri)
                {
                    var telemetryProperties = GetTelemetryProperties(catalogEntry);

                    using (_telemetryService.TrackDuration(TelemetryConstants.ProcessPackageDetailsSeconds, telemetryProperties))
                    {
                        var packageFileName = PackageUtility.GetPackageFileName(
                            packageId,
                            normalizedPackageVersion);
                        var sourceUri = new Uri(_contentBaseAddress, packageFileName);
                        var destinationStorage = _storageFactory.Create(packageId);
                        var destinationRelativeUri = DnxMaker.GetRelativeAddressNupkg(
                            packageId,
                            normalizedPackageVersion);
                        var destinationUri = destinationStorage.GetUri(destinationRelativeUri);

                        var isNupkgSynchronized = await destinationStorage.AreSynchronized(sourceUri, destinationUri);
                        var isPackageInIndex = await _dnxMaker.HasPackageInIndexAsync(
                            destinationStorage,
                            packageId,
                            normalizedPackageVersion,
                            cancellationToken);
                        var areRequiredPropertiesPresent = await AreRequiredPropertiesPresentAsync(destinationStorage, destinationUri);

                        if (isNupkgSynchronized && isPackageInIndex && areRequiredPropertiesPresent)
                        {
                            _logger.LogInformation("No changes detected: {Id}/{Version}", packageId, normalizedPackageVersion);

                            return;
                        }

                        if ((isNupkgSynchronized && areRequiredPropertiesPresent)
                            || await ProcessPackageDetailsAsync(
                                client,
                                packageId,
                                normalizedPackageVersion,
                                sourceUri,
                                catalogEntry.Uri,
                                telemetryProperties,
                                cancellationToken))
                        {
                            processedCatalogEntries.Add(catalogEntry);
                        }
                    }
                }
                else if (catalogEntry.Type.AbsoluteUri == Schema.DataTypes.PackageDelete.AbsoluteUri)
                {
                    var properties = GetTelemetryProperties(catalogEntry);

                    using (_telemetryService.TrackDuration(TelemetryConstants.ProcessPackageDeleteSeconds, properties))
                    {
                        await ProcessPackageDeleteAsync(packageId, normalizedPackageVersion, cancellationToken);

                        processedCatalogEntries.Add(catalogEntry);
                    }
                }
            });

            return processedCatalogEntries;
        }

        private async Task<bool> AreRequiredPropertiesPresentAsync(Storage destinationStorage, Uri destinationUri)
        {
            var azureStorage = destinationStorage as IAzureStorage;

            if (azureStorage == null)
            {
                return true;
            }

            return await azureStorage.HasPropertiesAsync(
                destinationUri,
                DnxConstants.ApplicationOctetStreamContentType,
                DnxConstants.DefaultCacheControl);
        }

        private async Task UpdatePackageVersionIndexAsync(
            IEnumerable<CatalogEntry> catalogEntries,
            CancellationToken cancellationToken)
        {
            var catalogEntryGroups = catalogEntries.GroupBy(catalogEntry => catalogEntry.PackageId);

            await catalogEntryGroups.ForEachAsync(_maxConcurrentCommitItemsWithinBatch, async catalogEntryGroup =>
            {
                var packageId = catalogEntryGroup.Key;
                var properties = new Dictionary<string, string>()
                {
                    { TelemetryConstants.Id, packageId },
                    { TelemetryConstants.BatchItemCount, catalogEntryGroup.Count().ToString() }
                };

                using (_telemetryService.TrackDuration(TelemetryConstants.ProcessPackageVersionIndexSeconds, properties))
                {
                    await _dnxMaker.UpdatePackageVersionIndexAsync(packageId, versions =>
                    {
                        foreach (var catalogEntry in catalogEntryGroup)
                        {
                            if (catalogEntry.Type.AbsoluteUri == Schema.DataTypes.PackageDetails.AbsoluteUri)
                            {
                                versions.Add(NuGetVersion.Parse(catalogEntry.NormalizedPackageVersion));
                            }
                            else if (catalogEntry.Type.AbsoluteUri == Schema.DataTypes.PackageDelete.AbsoluteUri)
                            {
                                versions.Remove(NuGetVersion.Parse(catalogEntry.NormalizedPackageVersion));
                            }
                        }
                    }, cancellationToken);
                }

                foreach (var catalogEntry in catalogEntryGroup)
                {
                    _logger.LogInformation("Commit: {Id}/{Version}", packageId, catalogEntry.NormalizedPackageVersion);
                }
            });
        }

        private async Task<bool> ProcessPackageDetailsAsync(
            HttpClient client,
            string packageId,
            string normalizedPackageVersion,
            Uri sourceUri,
            Uri catalogLeafUri,
            Dictionary<string, string> telemetryProperties,
            CancellationToken cancellationToken)
        {
            var catalogClient = _catalogClientFactory(client);
            var catalogLeaf = await catalogClient.GetPackageDetailsLeafAsync(catalogLeafUri.AbsoluteUri);

            if (await ProcessPackageDetailsViaStorageAsync(
                packageId,
                normalizedPackageVersion,
                catalogLeaf,
                telemetryProperties,
                cancellationToken))
            {
                return true;
            }

            _telemetryService.TrackMetric(
                TelemetryConstants.UsePackageSourceFallback,
                metric: 1,
                properties: GetTelemetryProperties(packageId, normalizedPackageVersion));

            return await ProcessPackageDetailsViaHttpAsync(
                client,
                packageId,
                normalizedPackageVersion,
                sourceUri,
                catalogLeaf,
                telemetryProperties,
                cancellationToken);
        }

        private async Task<bool> ProcessPackageDetailsViaStorageAsync(
            string packageId,
            string normalizedPackageVersion,
            PackageDetailsCatalogLeaf catalogLeaf,
            Dictionary<string, string> telemetryProperties,
            CancellationToken cancellationToken)
        {
            if (_sourceStorage == null)
            {
                return false;
            }

            var packageFileName = PackageUtility.GetPackageFileName(packageId, normalizedPackageVersion);
            var sourceUri = _sourceStorage.ResolveUri(packageFileName);

            var sourceBlob = await _sourceStorage.GetCloudBlockBlobReferenceAsync(sourceUri);

            if (await sourceBlob.ExistsAsync(cancellationToken))
            {
                // It's possible (though unlikely) that the blob may change between reads.  Reading a blob with a
                // single GET request returns the whole blob in a consistent state, but we're reading the blob many
                // different times.  To detect the blob changing between reads, we check the ETag again later.
                // If the ETag's differ, we'll fall back to using a single HTTP GET request.
                var token1 = await _sourceStorage.GetOptimisticConcurrencyControlTokenAsync(sourceUri, cancellationToken);

                telemetryProperties[TelemetryConstants.SizeInBytes] = sourceBlob.Length.ToString();

                var nuspec = await GetNuspecAsync(sourceBlob, packageId, cancellationToken);

                if (string.IsNullOrEmpty(nuspec))
                {
                    _logger.LogWarning(
                        "No .nuspec available for {Id}/{Version}.  Falling back to HTTP processing.",
                        packageId,
                        normalizedPackageVersion);
                }
                else
                {
                    await _dnxMaker.AddPackageAsync(
                        _sourceStorage,
                        nuspec,
                        packageId,
                        normalizedPackageVersion,
                        catalogLeaf.IconFilename,
                        cancellationToken);

                    var token2 = await _sourceStorage.GetOptimisticConcurrencyControlTokenAsync(sourceUri, cancellationToken);

                    if (token1 == token2)
                    {
                        _logger.LogInformation("Added .nupkg and .nuspec for package {Id}/{Version}", packageId, normalizedPackageVersion);

                        return true;
                    }
                    else
                    {
                        _telemetryService.TrackMetric(
                            TelemetryConstants.BlobModified,
                            metric: 1,
                            properties: GetTelemetryProperties(packageId, normalizedPackageVersion));
                    }
                }
            }
            else
            {
                _telemetryService.TrackMetric(
                    TelemetryConstants.NonExistentBlob,
                    metric: 1,
                    properties: GetTelemetryProperties(packageId, normalizedPackageVersion));
            }

            return false;
        }

        private async Task<bool> ProcessPackageDetailsViaHttpAsync(
            HttpClient client,
            string id,
            string version,
            Uri sourceUri,
            PackageDetailsCatalogLeaf catalogLeaf,
            Dictionary<string, string> telemetryProperties,
            CancellationToken cancellationToken)
        {
            var packageDownloader = new PackageDownloader(client, _logger);
            var requestUri = Utilities.GetNugetCacheBustingUri(sourceUri);

            using (var stream = await packageDownloader.DownloadAsync(requestUri, cancellationToken))
            {
                if (stream == null)
                {
                    _logger.LogWarning("Package {Id}/{Version} not found.", id, version);

                    return false;
                }

                telemetryProperties[TelemetryConstants.SizeInBytes] = stream.Length.ToString();

                var nuspec = GetNuspec(stream, id);

                if (nuspec == null)
                {
                    _logger.LogWarning("No .nuspec available for {Id}/{Version}. Skipping.", id, version);

                    return false;
                }

                stream.Position = 0;

                await _dnxMaker.AddPackageAsync(
                    stream,
                    nuspec,
                    id,
                    version,
                    catalogLeaf.IconFilename,
                    cancellationToken);
            }

            _logger.LogInformation("Added .nupkg and .nuspec for package {Id}/{Version}", id, version);

            return true;
        }

        private async Task ProcessPackageDeleteAsync(
            string packageId,
            string normalizedPackageVersion,
            CancellationToken cancellationToken)
        {
            await _dnxMaker.UpdatePackageVersionIndexAsync(
                packageId,
                versions => versions.Remove(NuGetVersion.Parse(normalizedPackageVersion)),
                cancellationToken);

            await _dnxMaker.DeletePackageAsync(packageId, normalizedPackageVersion, cancellationToken);

            _logger.LogInformation("Commit delete: {Id}/{Version}", packageId, normalizedPackageVersion);
        }

        private static void AssertNoMultipleEntriesForSamePackageIdentity(
            DateTime commitTimeStamp,
            IEnumerable<CatalogEntry> catalogEntries)
        {
            var catalogEntriesForSamePackageIdentity = catalogEntries.GroupBy(
                catalogEntry => new
                {
                    catalogEntry.PackageId,
                    catalogEntry.NormalizedPackageVersion
                })
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key.PackageId} {group.Key.NormalizedPackageVersion}");

            if (catalogEntriesForSamePackageIdentity.Any())
            {
                var packageIdentities = string.Join(", ", catalogEntriesForSamePackageIdentity);

                throw new InvalidOperationException($"The catalog batch {commitTimeStamp} contains multiple entries for the same package identity.  Package(s):  {packageIdentities}");
            }
        }

        private async Task<string> GetNuspecAsync(
            ICloudBlockBlob sourceBlob,
            string packageId,
            CancellationToken cancellationToken)
        {
            using (var stream = await sourceBlob.GetStreamAsync(cancellationToken))
            {
                return GetNuspec(stream, packageId);
            }
        }

        private static string GetNuspec(Stream stream, string id)
        {
            string name = $"{id}.nuspec";

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
            {
                // first look for a nuspec file named as the package id
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        using (TextReader reader = new StreamReader(entry.Open()))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
                // failing that, just return the first file that appears to be a nuspec
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".nuspec", StringComparison.InvariantCultureIgnoreCase))
                    {
                        using (TextReader reader = new StreamReader(entry.Open()))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }

            return null;
        }

        private static Dictionary<string, string> GetTelemetryProperties(CatalogEntry catalogEntry)
        {
            return GetTelemetryProperties(catalogEntry.PackageId, catalogEntry.NormalizedPackageVersion);
        }

        private static Dictionary<string, string> GetTelemetryProperties(string packageId, string normalizedPackageVersion)
        {
            return new Dictionary<string, string>()
            {
                { TelemetryConstants.Id, packageId },
                { TelemetryConstants.Version, normalizedPackageVersion }
            };
        }

        private async Task ProcessBatchAsync(
            CollectorHttpClient client,
            JToken context,
            string packageId,
            CatalogCommitItemBatch batch,
            CatalogCommitItemBatch lastBatch,
            CancellationToken cancellationToken)
        {
            await Task.Yield();

            using (_telemetryService.TrackDuration(
                TelemetryConstants.ProcessBatchSeconds,
                new Dictionary<string, string>()
                {
                    { TelemetryConstants.Id, packageId },
                    { TelemetryConstants.BatchItemCount, batch.Items.Count.ToString() }
                }))
            {
                await OnProcessBatchAsync(
                    client,
                    batch.Items,
                    context,
                    batch.CommitTimeStamp,
                    isLastBatch: false,
                    cancellationToken: cancellationToken);
            }
        }

        private sealed class CatalogEntry
        {
            internal DateTime CommitTimeStamp { get; }
            internal string PackageId { get; }
            internal string NormalizedPackageVersion { get; }
            internal Uri Type { get; }
            internal Uri Uri { get; }

            private CatalogEntry(DateTime commitTimeStamp, string packageId, string normalizedPackageVersion, Uri type, Uri uri)
            {
                CommitTimeStamp = commitTimeStamp;
                PackageId = packageId;
                NormalizedPackageVersion = normalizedPackageVersion;
                Type = type;
                Uri = uri;
            }

            internal static CatalogEntry Create(CatalogCommitItem item)
            {
                var typeUri = item.TypeUris.Single(uri =>
                    uri.AbsoluteUri == Schema.DataTypes.PackageDetails.AbsoluteUri ||
                    uri.AbsoluteUri == Schema.DataTypes.PackageDelete.AbsoluteUri);

                return new CatalogEntry(
                    item.CommitTimeStamp,
                    item.PackageIdentity.Id.ToLowerInvariant(),
                    item.PackageIdentity.Version.ToNormalizedString().ToLowerInvariant(),
                    typeUri,
                    item.Uri);
            }
        }
    }
}
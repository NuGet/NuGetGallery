// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    public class DnxCatalogCollector : CommitCollector
    {
        private readonly StorageFactory _storageFactory;
        private readonly DnxMaker _dnxMaker;
        private readonly ILogger _logger;
        private readonly int _maxDegreeOfParallelism;

        public DnxCatalogCollector(
            Uri index,
            StorageFactory storageFactory,
            ITelemetryService telemetryService,
            ILogger logger,
            int maxDegreeOfParallelism,
            Func<HttpMessageHandler> handlerFunc = null,
            TimeSpan? httpClientTimeout = null)
            : base(index, telemetryService, handlerFunc, httpClientTimeout)
        {
            _storageFactory = storageFactory ?? throw new ArgumentNullException(nameof(storageFactory));
            _dnxMaker = new DnxMaker(storageFactory);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (maxDegreeOfParallelism < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDegreeOfParallelism),
                    string.Format(Strings.ArgumentOutOfRange, 1, int.MaxValue));
            }

            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        public Uri ContentBaseAddress { get; set; }

        protected override async Task<bool> OnProcessBatch(
            CollectorHttpClient client,
            IEnumerable<JToken> items,
            JToken context,
            DateTime commitTimeStamp,
            bool isLastBatch,
            CancellationToken cancellationToken)
        {
            var catalogEntries = items.Select(
                    item => new CatalogEntry(
                        item["nuget:id"].ToString().ToLowerInvariant(),
                        NuGetVersionUtility.NormalizeVersion(item["nuget:version"].ToString().ToLowerInvariant()),
                        item["@type"].ToString().Replace("nuget:", Schema.Prefixes.NuGet),
                        item))
                .ToList();

            // Sanity check:  a single catalog batch should not contain multiple entries for the same package ID and version.
            AssertNoMultipleEntriesForSamePackageIdentity(commitTimeStamp, catalogEntries);

            // Process .nupkg/.nuspec adds and deletes.
            var processedCatalogEntries = await ProcessCatalogEntriesAsync(client, catalogEntries, cancellationToken);

            // Update the package version index with adds and deletes.
            await UpdatePackageVersionIndexAsync(processedCatalogEntries, cancellationToken);

            return true;
        }

        private async Task<IEnumerable<CatalogEntry>> ProcessCatalogEntriesAsync(CollectorHttpClient client, IEnumerable<CatalogEntry> catalogEntries, CancellationToken cancellationToken)
        {
            var processedCatalogEntries = new ConcurrentBag<CatalogEntry>();

            await catalogEntries.ForEachAsync(_maxDegreeOfParallelism, async catalogEntry =>
            {
                var packageId = catalogEntry.PackageId;
                var packageVersion = catalogEntry.PackageVersion;

                if (catalogEntry.EntryType == Schema.DataTypes.PackageDetails.ToString())
                {
                    var properties = GetTelemetryProperties(packageId, packageVersion);

                    using (_telemetryService.TrackDuration(TelemetryConstants.ProcessPackageDetailsSeconds, properties))
                    {
                        var sourceUri = new Uri(ContentBaseAddress, $"{packageId}.{packageVersion}.nupkg");
                        var destinationStorage = _storageFactory.Create(packageId);
                        var destinationUri = destinationStorage.GetUri(DnxMaker.GetRelativeAddressNupkg(packageId, packageVersion));

                        var isNupkgSynchronized = await destinationStorage.AreSynchronized(sourceUri, destinationUri);
                        var isPackageInIndex = await _dnxMaker.HasPackageInIndexAsync(destinationStorage, packageId, packageVersion, cancellationToken);

                        if (isNupkgSynchronized && isPackageInIndex)
                        {
                            _logger.LogInformation("No changes detected: {Id}/{Version}", packageId, packageVersion);
                            return;
                        }

                        if (isNupkgSynchronized || await ProcessPackageDetailsAsync(client, packageId, packageVersion, sourceUri, cancellationToken))
                        {
                            processedCatalogEntries.Add(catalogEntry);
                        }
                    }
                }
                else if (catalogEntry.EntryType == Schema.DataTypes.PackageDelete.ToString())
                {
                    var properties = GetTelemetryProperties(packageId, packageVersion);

                    using (_telemetryService.TrackDuration(TelemetryConstants.ProcessPackageDeleteSeconds, properties))
                    {
                        await ProcessPackageDeleteAsync(packageId, packageVersion, cancellationToken);

                        processedCatalogEntries.Add(catalogEntry);
                    }
                }
            });

            return processedCatalogEntries;
        }

        private async Task UpdatePackageVersionIndexAsync(IEnumerable<CatalogEntry> catalogEntries, CancellationToken cancellationToken)
        {
            var catalogEntryGroups = catalogEntries.GroupBy(catalogEntry => catalogEntry.PackageId);

            await catalogEntryGroups.ForEachAsync(_maxDegreeOfParallelism, async catalogEntryGroup =>
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
                            if (catalogEntry.EntryType == Schema.DataTypes.PackageDetails.ToString())
                            {
                                versions.Add(NuGetVersion.Parse(catalogEntry.PackageVersion));
                            }
                            else if (catalogEntry.EntryType == Schema.DataTypes.PackageDelete.ToString())
                            {
                                versions.Remove(NuGetVersion.Parse(catalogEntry.PackageVersion));
                            }
                        }
                    }, cancellationToken);
                }

                foreach (var catalogEntry in catalogEntryGroup)
                {
                    _logger.LogInformation("Commit: {Id}/{Version}", packageId, catalogEntry.PackageVersion);
                }
            });
        }

        private async Task<bool> ProcessPackageDetailsAsync(
            HttpClient client,
            string id,
            string version,
            Uri sourceUri,
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
                    cancellationToken);
            }

            _logger.LogInformation("Added .nupkg and .nuspec for package {Id}/{Version}", id, version);

            return true;
        }

        private async Task ProcessPackageDeleteAsync(string id, string version, CancellationToken cancellationToken)
        {
            await _dnxMaker.UpdatePackageVersionIndexAsync(
                id,
                versions => versions.Remove(NuGetVersion.Parse(version)),
                cancellationToken);

            await _dnxMaker.DeletePackageAsync(id, version, cancellationToken);

            _logger.LogInformation("Commit delete: {Id}/{Version}", id, version);
        }

        private static void AssertNoMultipleEntriesForSamePackageIdentity(DateTime commitTimeStamp, IEnumerable<CatalogEntry> catalogEntries)
        {
            var catalogEntriesForSamePackageIdentity = catalogEntries.GroupBy(
                catalogEntry => new
                {
                    catalogEntry.PackageId,
                    catalogEntry.PackageVersion
                })
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key.PackageId} {group.Key.PackageVersion}");

            if (catalogEntriesForSamePackageIdentity.Any())
            {
                var packageIdentities = string.Join(", ", catalogEntriesForSamePackageIdentity);

                throw new InvalidOperationException($"The catalog batch {commitTimeStamp} contains multiple entries for the same package identity.  Package(s):  {packageIdentities}");
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

        private static Dictionary<string, string> GetTelemetryProperties(string packageId, string packageVersion)
        {
            return new Dictionary<string, string>()
            {
                { TelemetryConstants.Id, packageId },
                { TelemetryConstants.Version, packageVersion }
            };
        }

        private sealed class CatalogEntry
        {
            internal string PackageId { get; }
            internal string PackageVersion { get; }
            internal string EntryType { get; }
            internal JToken Entry { get; }

            internal CatalogEntry(string packageId, string packageVersion, string entryType, JToken entry)
            {
                PackageId = packageId;
                PackageVersion = packageVersion;
                EntryType = entryType;
                Entry = entry;
            }
        }
    }
}
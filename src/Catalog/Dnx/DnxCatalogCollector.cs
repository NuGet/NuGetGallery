// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Dnx
{
    public class DnxCatalogCollector : CommitCollector
    {
        private readonly StorageFactory _storageFactory;
        private readonly DnxMaker _dnxMaker;
        private readonly ILogger _logger;

        public DnxCatalogCollector(
            Uri index,
            StorageFactory storageFactory,
            ITelemetryService telemetryService,
            ILogger logger,
            Func<HttpMessageHandler> handlerFunc = null,
            TimeSpan? httpClientTimeout = null)
            : base(index, telemetryService, handlerFunc, httpClientTimeout)
        {
            _storageFactory = storageFactory;
            _dnxMaker = new DnxMaker(storageFactory);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Uri ContentBaseAddress { get; set; }

        protected override async Task<bool> OnProcessBatch(CollectorHttpClient client, IEnumerable<JToken> items, JToken context, DateTime commitTimeStamp, bool isLastBatch, CancellationToken cancellationToken)
        {
            foreach (JToken item in items)
            {
                string id = item["nuget:id"].ToString().ToLowerInvariant();
                string version = NuGetVersionUtility.NormalizeVersion(item["nuget:version"].ToString().ToLowerInvariant());
                string type = item["@type"].ToString().Replace("nuget:", Schema.Prefixes.NuGet);

                if (type == Schema.DataTypes.PackageDetails.ToString())
                {
                    var properties = GetTelemetryProperties(id, version);

                    using (_telemetryService.TrackDuration(TelemetryConstants.ProcessPackageDetails, properties))
                    {
                        await ProcessPackageDetailsAsync(client, id, version, cancellationToken);
                    }
                }
                else if (type == Schema.DataTypes.PackageDelete.ToString())
                {
                    var properties = GetTelemetryProperties(id, version);

                    using (_telemetryService.TrackDuration(TelemetryConstants.ProcessPackageDelete, properties))
                    {
                        await ProcessPackageDeleteAsync(id, version, cancellationToken);
                    }
                }
            }

            return true;
        }

        private async Task ProcessPackageDetailsAsync(
            HttpClient client,
            string id,
            string version,
            CancellationToken cancellationToken)
        {
            var sourceUri = new Uri(ContentBaseAddress, string.Format("{0}.{1}.nupkg", id, version));

            var destinationStorage = _storageFactory.Create(id);
            var destinationUri = destinationStorage.GetUri(DnxMaker.GetRelativeAddressNupkg(id, version));

            var isNupkgSynchronized = await destinationStorage.AreSynchronized(sourceUri, destinationUri);
            if (isNupkgSynchronized
                && await _dnxMaker.HasPackageInIndex(destinationStorage, id, version, cancellationToken))
            {
                _logger.LogInformation("No changes detected: {Id}/{Version}", id, version);
                return;
            }

            if (isNupkgSynchronized)
            {
                _logger.LogInformation(
                    "The .nuspec and .nupkg for {Id}/{Version} are already uploaded. Updating just the index.json.",
                    id,
                    version);

                await _dnxMaker.AddPackageToIndex(
                    id,
                    version,
                    cancellationToken);
            }
            else
            {
                var packageDownloader = new PackageDownloader(client, _logger);
                var requestUri = Utilities.GetNugetCacheBustingUri(sourceUri);

                using (var stream = await packageDownloader.DownloadAsync(requestUri, cancellationToken))
                {
                    if (stream == null)
                    {
                        _logger.LogWarning("Package {Id}/{Version} not found.", id, version);
                        return;
                    }

                    var nuspec = GetNuspec(stream, id);
                    if (nuspec == null)
                    {
                        _logger.LogWarning("No .nuspec available for {Id}/{Version}. Skipping.", id, version);
                        return;
                    }

                    stream.Position = 0;

                    await _dnxMaker.AddPackage(
                        stream,
                        nuspec,
                        id,
                        version,
                        cancellationToken);
                }
            }

            _logger.LogInformation("Commit: {Id}/{Version}", id, version);
        }

        private async Task ProcessPackageDeleteAsync(string id, string version, CancellationToken cancellationToken)
        {
            await _dnxMaker.DeletePackage(id, version, cancellationToken);

            _logger.LogInformation("Commit delete: {Id}/{Version}", id, version);
        }

        private static string GetNuspec(Stream stream, string id)
        {
            string name = string.Format("{0}.nuspec", id);
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
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
    }
}
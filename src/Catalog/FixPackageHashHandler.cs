// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// Adds the Content MD5 property to package blobs that are missing it.
    /// </summary>
    public class FixPackageHashHandler : IPackagesContainerHandler
    {
        private readonly HttpClient _httpClient;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<FixPackageHashHandler> _logger;

        public FixPackageHashHandler(
            HttpClient httpClient,
            ITelemetryService telemetryService,
            ILogger<FixPackageHashHandler> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessPackageAsync(CatalogIndexEntry packageEntry, ICloudBlockBlob blob)
        {
            await blob.FetchAttributesAsync(CancellationToken.None);

            // Skip the package if it has a Content MD5 hash
            if (blob.ContentMD5 != null)
            {
                _telemetryService.TrackPackageAlreadyHasHash(packageEntry.Id, packageEntry.Version);
                return;
            }

            // Download the blob and calculate its hash. We use HttpClient to download blobs as Azure Blob Sotrage SDK
            // occassionally hangs. See: https://github.com/Azure/azure-storage-net/issues/470
            string hash;
            using (var hashAlgorithm = MD5.Create())
            using (var packageStream = await _httpClient.GetStreamAsync(blob.Uri))
            {
                var hashBytes = hashAlgorithm.ComputeHash(packageStream);

                hash = Convert.ToBase64String(hashBytes);
            }

            blob.ContentMD5 = hash;

            var condition = AccessCondition.GenerateIfMatchCondition(blob.ETag);
            await blob.SetPropertiesAsync(
                condition,
                options: null,
                operationContext: null);

            _telemetryService.TrackPackageHashFixed(packageEntry.Id, packageEntry.Version);

            _logger.LogWarning(
                "Updated package {PackageId} {PackageVersion}, set hash to '{Hash}' using ETag {ETag}",
                packageEntry.Id,
                packageEntry.Version,
                hash,
                blob.ETag);
        }
    }
}

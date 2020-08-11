// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// Validates that packages in the packages container have the correct MD5 Content hash on their blob's properties.
    /// </summary>
    public class ValidatePackageHashHandler : IPackagesContainerHandler
    {
        private readonly HttpClient _httpClient;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<ValidatePackageHashHandler> _logger;

        public ValidatePackageHashHandler(
            HttpClient httpClient,
            ITelemetryService telemetryService,
            ILogger<ValidatePackageHashHandler> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessPackageAsync(CatalogIndexEntry packageEntry, ICloudBlockBlob blob)
        {
            await blob.FetchAttributesAsync(CancellationToken.None);

            if (blob.ContentMD5 == null)
            {
                _telemetryService.TrackPackageMissingHash(packageEntry.Id, packageEntry.Version);

                _logger.LogError(
                    "Package {PackageId} {PackageVersion} has a null Content MD5 hash!",
                    packageEntry.Id,
                    packageEntry.Version);
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

            if (blob.ContentMD5 != hash)
            {
                _telemetryService.TrackPackageHasIncorrectHash(packageEntry.Id, packageEntry.Version);

                _logger.LogError(
                    "Package {PackageId} {PackageVersion} has an incorrect Content MD5 hash! Expected: '{ExpectedHash}', actual: '{ActualHash}'",
                    packageEntry.Id,
                    packageEntry.Version,
                    hash,
                    blob.ContentMD5);
            }
        }
    }
}

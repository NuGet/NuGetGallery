// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog
{
    /// <summary>
    /// A processor that does work on a package's blob in the packages container.
    /// </summary>
    public class PackagesContainerCatalogProcessor : ICatalogIndexProcessor
    {
        private const int MaximumPackageProcessingAttempts = 5;

        private readonly CloudBlobContainer _container;
        private readonly IPackagesContainerHandler _handler;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<PackagesContainerCatalogProcessor> _logger;

        /// <summary>
        /// Create a new packages container processor.
        /// </summary>
        /// <param name="container">The reference to the packages container.</param>
        /// <param name="handler">The handler that will be run on blobs in the packages container.</param>
        /// <param name="telemetryService">The service used to track events.</param>
        /// <param name="logger"></param>
        public PackagesContainerCatalogProcessor(
            CloudBlobContainer container,
            IPackagesContainerHandler handler,
            ITelemetryService telemetryService,
            ILogger<PackagesContainerCatalogProcessor> logger)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessCatalogIndexEntryAsync(CatalogIndexEntry catalogEntry)
        {
            try
            {
                var rawBlob = _container.GetBlockBlobReference(BuildPackageFileName(catalogEntry));
                var blob = new AzureCloudBlockBlob(rawBlob);

                for (int i = 0; i < MaximumPackageProcessingAttempts; i++)
                {
                    try
                    {
                        await _handler.ProcessPackageAsync(catalogEntry, blob);
                        return;
                    }
                    catch (Exception e) when (IsRetryableException(e))
                    {
                        _logger.LogWarning(
                            0,
                            e,
                            "Processing package {PackageId} {PackageVersion} failed due to an uncaught exception. " +
                            $"Attempt {{Attempt}} of {MaximumPackageProcessingAttempts}",
                            catalogEntry.Id,
                            catalogEntry.Version,
                            i + 1);
                    }
                }

                _telemetryService.TrackHandlerFailedToProcessPackage(_handler, catalogEntry.Id, catalogEntry.Version);

                _logger.LogError(
                    $"Failed to process package {{PackageId}} {{PackageVersion}} after {MaximumPackageProcessingAttempts} attempts",
                    catalogEntry.Id,
                    catalogEntry.Version);
            }
            catch (StorageException e) when (IsPackageDoesNotExistException(e))
            {
                // This indicates a discrepancy between v2 and v3 APIs that should be caught by
                // the monitoring job. No need to track this handler failure.
                _logger.LogError(
                    "Package {PackageId} {PackageVersion} is missing from the packages container!",
                    catalogEntry.Id,
                    catalogEntry.Version);
            }
            catch (Exception e)
            {
                _telemetryService.TrackHandlerFailedToProcessPackage(_handler, catalogEntry.Id, catalogEntry.Version);

                _logger.LogError(
                    0,
                    e,
                    "Could not process package {PackageId} {PackageVersion}",
                    catalogEntry.Id,
                    catalogEntry.Version);
            }
        }

        private string BuildPackageFileName(CatalogIndexEntry packageEntry)
        {
            var packageId = packageEntry.Id.ToLowerInvariant();
            var packageVersion = packageEntry.Version.ToNormalizedString().ToLowerInvariant();

            return $"{packageId}.{packageVersion}.nupkg";
        }

        private static bool IsRetryableException(Exception exception)
        {
            // Retry on HTTP Client timeouts.
            if (exception is TaskCanceledException)
            {
                return true;
            }

            // Retry if updating a blob fails due to an incorrect ETag.
            if (exception is StorageException storageException && IsPackageWasModifiedException(storageException))
            {
                return true;
            }

            do
            {
                // Retry on time out exceptions.
                if (exception is TimeoutException)
                {
                    return true;
                }

                if (exception is WebException we && we.Status == WebExceptionStatus.Timeout)
                {
                    return true;
                }

                // Retry if the host forcibly closes the connection. 
                if (exception is SocketException)
                {
                    return true;
                }

                exception = exception.InnerException;
            }
            while (exception != null);

            return false;
        }

        private static bool IsPackageDoesNotExistException(StorageException exception)
        {
            return exception?.RequestInformation?.HttpStatusCode == (int?)HttpStatusCode.NotFound;
        }

        private static bool IsPackageWasModifiedException(StorageException exception)
        {
            return exception?.RequestInformation?.HttpStatusCode == (int?)HttpStatusCode.PreconditionFailed;
        }
    }
}

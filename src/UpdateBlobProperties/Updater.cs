// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace UpdateBlobProperties
{
    public class Updater : IUpdater
    {
        private const int MaxRetries = 3;

        private readonly BlobContainerClient _blobContainerClient;
        private readonly BlobInfo _blobInfo;
        private readonly ILogger<Updater> _logger;

        public Updater(BlobContainerClient blobContainerClient, BlobInfo blobInfo, ILogger<Updater> logger)
        {
            _blobContainerClient = blobContainerClient;
            _blobInfo = blobInfo;
            _logger = logger;
        }

        public async Task UpdateBlobPropertiesAsync(PackageInfo packageInfo, CancellationToken token)
        {
            var blobName = _blobInfo.GetBlobName(packageInfo);
            var blobClient = _blobContainerClient.GetBlobClient(blobName);

            // Retries for concurrency control (Blob client is enabled with the retry policy)
            var retries = 0;
            while (retries < MaxRetries)
            {
                try
                {
                    var response = await blobClient.GetPropertiesAsync(cancellationToken: token);
                    if (!TryGetHeadersWhenBlobPropertiesNotMatched(response.Value, _blobInfo.GetUpdatedBlobProperties(), out var blobHttpHeaders))
                    {
                        _logger.LogInformation("Blob properties of Package Id: {packageId} and Version: {packageVersion} (Blob Uri: {blobUri}) are matched.",
                            packageInfo.Id, packageInfo.Version, blobClient.Uri);

                        return;
                    }

                    if (await UpdateAsync(blobClient, blobHttpHeaders, response.Value.ETag, token))
                    {
                        _logger.LogInformation("Blob properties of Package Id: {packageId} and Version: {packageVersion} (Blob Uri: {blobUri}) are updated successfully.",
                            packageInfo.Id, packageInfo.Version, blobClient.Uri);

                        return;
                    }
                    else
                    {
                        _logger.LogInformation("The blob of Package Id: {packageId} and Version: {packageVersion} (Blob Uri: {blobUri}) has been updated since the last read.",
                            packageInfo.Id, packageInfo.Version, blobClient.Uri);
                    }
                }
                catch (RequestFailedException e) when (e.Status == (int)HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("The blob of Package Id: {packageId} and Version: {packageVersion} (Blob Uri: {blobUri}) does not exist.",
                        packageInfo.Id, packageInfo.Version, blobClient.Uri);

                    return;
                }

                retries++;
                if (retries == MaxRetries)
                {
                    throw new Exception($"Failed to update blob properties of Package Id: {packageInfo.Id} and Version: {packageInfo.Version} (Blob Uri: {blobClient.Uri}) after {MaxRetries} retries.");
                }

                await Task.Delay(TimeSpan.FromSeconds(retries * 5), token);
            }
        }

        private async Task<bool> UpdateAsync(BlobClient blobClient, BlobHttpHeaders blobHttpHeaders, ETag eTag, CancellationToken token)
        {
            try
            {
                await blobClient.SetHttpHeadersAsync(blobHttpHeaders, new BlobRequestConditions() { IfMatch = eTag }, cancellationToken: token);

                return true;
            }
            catch (RequestFailedException e) when (e.Status == (int)HttpStatusCode.PreconditionFailed)
            {
                return false;
            }
        }

        private bool TryGetHeadersWhenBlobPropertiesNotMatched(BlobProperties blobProperties, IDictionary<string, string> updatedBlobProperties,
            out BlobHttpHeaders blobHttpHeaders)
        {
            blobHttpHeaders = new BlobHttpHeaders
            {
                CacheControl = blobProperties.CacheControl,
                ContentType = blobProperties.ContentType,
                ContentHash = blobProperties.ContentHash,
                ContentEncoding = blobProperties.ContentEncoding,
                ContentLanguage = blobProperties.ContentLanguage,
                ContentDisposition = blobProperties.ContentDisposition,
            };

            var notMatched = false;
            if (updatedBlobProperties.TryGetValue(nameof(BlobProperties.CacheControl), out var value) &&
                blobProperties.CacheControl != value)
            {
                blobHttpHeaders.CacheControl = value;
                notMatched = true;
            }

            return notMatched;
        }
    }
}

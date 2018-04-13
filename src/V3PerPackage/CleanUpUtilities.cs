// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Services.V3PerPackage
{
    public static class CleanUpUtilities
    {
        public static async Task DeleteContainer(CloudBlobClient blobClient, string containerName, ILogger logger)
        {
            var container = blobClient.GetContainerReference(containerName);
            var deleted = await container.DeleteIfExistsAsync();

            if (deleted)
            {
                logger.LogInformation("Deleted container {ContainerName}.", containerName);
            }
        }

        public static string GetLuceneCacheDirectory()
        {
            return Path.Combine(
                Environment.ExpandEnvironmentVariables("%temp%"),
                "AzureDirectory");
        }

        public static async Task DeleteBlobsWithPrefix(CloudBlobClient blobClient, string containerName, string prefix, ILogger logger)
        {
            var container = blobClient.GetContainerReference(containerName);

            if (!(await container.ExistsAsync()))
            {
                return;
            }

            var blobs = container.ListBlobs(prefix, useFlatBlobListing: true);
            foreach (var blob in blobs.OfType<CloudBlockBlob>())
            {
                var deleted = await blob.DeleteIfExistsAsync(
                    DeleteSnapshotsOption.IncludeSnapshots,
                    accessCondition: null,
                    options: null,
                    operationContext: null);

                if (deleted)
                {
                    logger.LogInformation("Deleted blob {ContainerName}/{BlobName}.", blob.Container.Name, blob.Name);
                }
            }
        }
    }
}

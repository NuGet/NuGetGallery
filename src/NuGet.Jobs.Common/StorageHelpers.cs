// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Jobs
{
    public static class StorageHelpers
    {
        private const string _contentTypeJson = "application/json";
        private static readonly string PackageBackupsDirectory = "packages";
        private static readonly string PackageBlobNameFormat = "{0}.{1}.nupkg";
        private static readonly string PackageBackupBlobNameFormat = PackageBackupsDirectory + "/{0}/{1}/{2}.nupkg";

        public static string GetPackageBlobName(string id, string version)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                PackageBlobNameFormat,
                id,
                version).ToLowerInvariant();
        }

        public static string GetPackageBackupBlobName(string id, string version, string hash)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                PackageBackupBlobNameFormat,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                WebUtility.UrlEncode(hash));
        }

        public static CloudBlobDirectory GetBlobDirectory(CloudStorageAccount account, string path)
        {
            var client = account.CreateCloudBlobClient();
            client.DefaultRequestOptions = new BlobRequestOptions
            {
                ServerTimeout = TimeSpan.FromMinutes(5)
            };

            string[] segments = path.Split('/');
            string containerName;
            string prefix;

            if (segments.Length < 2)
            {
                // No "/" segments, so the path is a container and the catalog is at the root...
                containerName = path;
                prefix = string.Empty;
            }
            else
            {
                // Found "/" segments, but we need to get the first segment to use as the container...
                containerName = segments[0];
                prefix = string.Join("/", segments.Skip(1)) + "/";
            }

            var container = client.GetContainerReference(containerName);
            var dir = container.GetDirectoryReference(prefix);
            return dir;
        }

        public static async Task UploadJsonBlob(CloudBlobContainer container, string blobName, string content)
        {
            CloudBlockBlob blob = container.GetBlockBlobReference(blobName);
            blob.Properties.ContentType = _contentTypeJson;
            await blob.UploadTextAsync(content);
        }
    }
}


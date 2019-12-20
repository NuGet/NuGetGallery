// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using NuGet.Protocol;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Versioning;
using NuGetGallery;

namespace NuGet.Jobs.Catalog2Registration
{
    public class HiveStorage : IHiveStorage
    {
        private readonly ICloudBlobClient _cloudBlobClient;
        private readonly RegistrationUrlBuilder _urlBuilder;
        private readonly IEntityBuilder _entityBuilder;
        private readonly IThrottle _throttle;
        private readonly IOptionsSnapshot<Catalog2RegistrationConfiguration> _options;
        private readonly ILogger<HiveStorage> _logger;

        public HiveStorage(
            ICloudBlobClient cloudBlobClient,
            RegistrationUrlBuilder urlBuilder,
            IEntityBuilder entityBuilder,
            IThrottle throttle,
            IOptionsSnapshot<Catalog2RegistrationConfiguration> options,
            ILogger<HiveStorage> logger)
        {
            _cloudBlobClient = cloudBlobClient ?? throw new ArgumentNullException(nameof(cloudBlobClient));
            _urlBuilder = urlBuilder ?? throw new ArgumentNullException(nameof(urlBuilder));
            _entityBuilder = entityBuilder ?? throw new ArgumentNullException(nameof(entityBuilder));
            _throttle = throttle ?? throw new ArgumentNullException(nameof(throttle));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<RegistrationIndex> ReadIndexOrNullAsync(HiveType hive, string id)
        {
            var path = _urlBuilder.GetIndexPath(id);
            return await ReadAsync<RegistrationIndex>(hive, path, "index", allow404: true);
        }

        public async Task<RegistrationPage> ReadPageAsync(HiveType hive, string url)
        {
            var path = _urlBuilder.ConvertToPath(hive, url);
            return await ReadAsync<RegistrationPage>(hive, path, "page", allow404: false);
        }

        public async Task WriteIndexAsync(
            HiveType hive,
            IReadOnlyList<HiveType> replicaHives,
            string id,
            RegistrationIndex index)
        {
            var path = _urlBuilder.GetIndexPath(id);
            await WriteAsync(hive, replicaHives, path, index, "index", _entityBuilder.UpdateIndexUrls);
        }

        public async Task WritePageAsync(
            HiveType hive,
            IReadOnlyList<HiveType> replicaHives,
            string id,
            NuGetVersion lower,
            NuGetVersion upper,
            RegistrationPage page)
        {
            var path = _urlBuilder.GetPagePath(id, lower, upper);
            await WriteAsync(hive, replicaHives, path, page, "page", _entityBuilder.UpdatePageUrls);
        }

        public async Task WriteLeafAsync(
            HiveType hive,
            IReadOnlyList<HiveType> replicaHives,
            string id,
            NuGetVersion version,
            RegistrationLeaf leaf)
        {
            var path = _urlBuilder.GetLeafPath(id, version);
            await WriteAsync(hive, replicaHives, path, leaf, "leaf", _entityBuilder.UpdateLeafUrls);
        }

        public async Task DeleteIndexAsync(
            HiveType hive,
            IReadOnlyList<HiveType> replicaHives,
            string id)
        {
            var path = _urlBuilder.GetIndexPath(id);
            await DeleteAsync(hive, replicaHives, path);
        }

        public async Task DeleteUrlAsync(
            HiveType hive,
            IReadOnlyList<HiveType> replicaHives,
            string url)
        {
            var path = _urlBuilder.ConvertToPath(hive, url);
            await DeleteAsync(hive, replicaHives, path);
        }

        private async Task<T> ReadAsync<T>(
            HiveType hive,
            string path,
            string typeName,
            bool allow404)
        {
            var blob = GetBlobReference(hive, path);

            _logger.LogInformation(
                "Reading {TypeName} from container {Container} at path {Path}.",
                typeName,
                GetContainerName(hive),
                path);

            await _throttle.WaitAsync();
            try
            {
                T result;
                using (var blobStream = await blob.OpenReadAsync(AccessCondition.GenerateEmptyCondition()))
                {
                    Stream readStream;
                    if (blob.Properties.ContentEncoding == "gzip")
                    {
                        readStream = new GZipStream(blobStream, CompressionMode.Decompress);
                    }
                    else
                    {
                        readStream = blobStream;
                    }

                    using (readStream)
                    using (var streamReader = new StreamReader(readStream))
                    using (var jsonTextReader = new JsonTextReader(streamReader))
                    {
                        result = NuGetJsonSerialization.Serializer.Deserialize<T>(jsonTextReader);
                    }
                }

                _logger.LogInformation(
                    "Finished reading {TypeName} from container {Container} at path {Path} with Content-Encoding {ContentEncoding}.",
                    typeName,
                    GetContainerName(hive),
                    path,
                    blob.Properties.ContentEncoding);

                return result;
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                _logger.LogInformation(
                    "No blob in container {Container} at path {Path} exists.",
                    GetContainerName(hive),
                    path,
                    blob.Properties.ContentEncoding);

                if (allow404)
                {
                    return default(T);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                _throttle.Release();
            }
        }

        private MemoryStream Serialize<T>(HiveType hive, T entity)
        {
            var serializedStream = new MemoryStream();
            Stream writeStream;
            GZipStream gzipStream;
            if (IsGzipped(hive))
            {
                gzipStream = new GZipStream(
                    serializedStream,
                    CompressionMode.Compress,
                    leaveOpen: true);
                writeStream = gzipStream;
            }
            else
            {
                gzipStream = null;
                writeStream = serializedStream;
            }

            using (var streamWriter = new StreamWriter(
                writeStream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 1024,
                leaveOpen: true))
            using (var jsonTextWriter = new JsonTextWriter(streamWriter))
            {
                NuGetJsonSerialization.Serializer.Serialize(jsonTextWriter, entity);
            }

            gzipStream?.Dispose();
            serializedStream.Position = 0;
            return serializedStream;
        }

        private async Task WriteAsync<T>(
            HiveType hive,
            IReadOnlyList<HiveType> replicaHives,
            string path,
            T entity,
            string typeName,
            Action<T, HiveType, HiveType> updateUrls)
        {
            var hiveToWriteTask = new Dictionary<HiveType, Task>();
            var previousHive = hive;
            foreach (var currentHive in GetHiveSequence(hive, replicaHives))
            {
                if (hiveToWriteTask.ContainsKey(currentHive))
                {
                    continue;
                }

                if (currentHive != previousHive)
                {
                    // The hive defines the base URL. If we're now writing to another hive (which is the case if this
                    // is the 2nd ... Nth hive) then we need to update the URLs to point to the current hive.
                    updateUrls(entity, previousHive, currentHive);
                }

                // Serialize in sequence since we have a single entity that needs to be written to multiple hives and
                // therefore have multiple URL base addresses for the various serializations.
                var memoryStream = Serialize(currentHive, entity);

                // Write in parallel since we've captured the serialized bytes at this point and can safely proceed to
                // the next hive which will update the entity URLs.
                var writeTask = WriteAsync(currentHive, path, memoryStream, typeName);
                hiveToWriteTask.Add(currentHive, writeTask);

                previousHive = currentHive;
            }

            // Return the URLs to their original state so that the caller can consider replica hives as an
            // implementation detail and not need to worry about the entity changing during the process.
            if (previousHive != hive)
            {
                updateUrls(entity, previousHive, hive);
            }

            await Task.WhenAll(hiveToWriteTask.Values);
        }

        private static IEnumerable<HiveType> GetHiveSequence(HiveType hive, IReadOnlyList<HiveType> replicaHives)
        {
            yield return hive;
            foreach (var replicaHive in replicaHives)
            {
                yield return replicaHive;
            }
        }

        private async Task WriteAsync(
            HiveType hive,
            string path,
            MemoryStream memoryStream,
            string typeName)
        {
            using (memoryStream)
            {
                var container = GetContainer(hive);
                var blob = GetBlobReference(container, path);

                blob.Properties.ContentType = "application/json";
                blob.Properties.CacheControl = "no-store";

                if (IsGzipped(hive))
                {
                    blob.Properties.ContentEncoding = "gzip";
                }

                _logger.LogInformation(
                    "Writing {TypeName} ({ByteCount} bytes) to container {Container} at path {Path} with Content-Encoding {ContentEncoding}.",
                    typeName,
                    memoryStream.Length,
                    GetContainerName(hive),
                    path,
                    blob.Properties.ContentEncoding);

                await _throttle.WaitAsync();
                try
                {
                    await blob.UploadFromStreamAsync(memoryStream, AccessCondition.GenerateEmptyCondition());

                    if (_options.Value.EnsureSingleSnapshot)
                    {
                        var segment = await container.ListBlobsSegmentedAsync(
                            path,
                            useFlatBlobListing: true,
                            blobListingDetails: BlobListingDetails.Snapshots,
                            maxResults: 2,
                            blobContinuationToken: null,
                            options: null,
                            operationContext: null,
                            cancellationToken: CancellationToken.None);

                        if (segment.Results.Count == 1)
                        {
                            _logger.LogInformation(
                                "The {TypeName} blob in container {Container} at path {Path} does not have a snapshot so one will be created.",
                                typeName,
                                GetContainerName(hive),
                                path);
                            await blob.SnapshotAsync(CancellationToken.None);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "The {TypeName} blob in container {Container} at path {Path} already has a snapshot.",
                                typeName,
                                GetContainerName(hive),
                                path);
                        }
                    }
                }
                finally
                {
                    _throttle.Release();
                }

                _logger.LogInformation(
                    "Finished writing {TypeName} to container {Container} and path {Path}.",
                    typeName,
                    GetContainerName(hive),
                    path);
            }
        }

        private async Task DeleteAsync(HiveType hive, IReadOnlyList<HiveType> replicaHives, string path)
        {
            var deleteTasks = GetHiveSequence(hive, replicaHives)
                .Select(x => DeleteAsync(x, path))
                .ToList();
            await Task.WhenAll(deleteTasks);
        }

        private async Task DeleteAsync(HiveType hive, string path)
        {
            var blob = GetBlobReference(hive, path);

            await _throttle.WaitAsync();
            try
            {
                if (await blob.ExistsAsync())
                {
                    _logger.LogInformation(
                        "Deleting blob in container {Container} at path {Path}.",
                        GetContainerName(hive),
                        path);
                    await blob.DeleteIfExistsAsync();
                    _logger.LogInformation(
                        "Finished deleting blob in container {Container} at path {Path}.",
                        GetContainerName(hive),
                        path);
                }
                else
                {
                    _logger.LogInformation(
                        "No blob in container {Container} at path {Path} exists so no delete is required.",
                        GetContainerName(hive),
                        path);
                }
            }
            finally
            {
                _throttle.Release();
            }
        }

        private ISimpleCloudBlob GetBlobReference(ICloudBlobContainer container, string path)
        {
            var blob = container.GetBlobReference(Uri.UnescapeDataString(path));
            return blob;
        }

        private ISimpleCloudBlob GetBlobReference(HiveType hive, string path)
        {
            var container = GetContainer(hive);
            return GetBlobReference(container, path);
        }

        private bool IsGzipped(HiveType hive)
        {
            return hive != HiveType.Legacy;
        }

        private string GetContainerName(HiveType hive)
        {
            string container;
            switch (hive)
            {
                case HiveType.Legacy:
                    container = _options.Value.LegacyStorageContainer;
                    break;
                case HiveType.Gzipped:
                    container = _options.Value.GzippedStorageContainer;
                    break;
                case HiveType.SemVer2:
                    container = _options.Value.SemVer2StorageContainer;
                    break;
                default:
                    throw new NotImplementedException($"The hive type '{hive}' does not have configured storage container.");
            }

            return container;
        }

        private ICloudBlobContainer GetContainer(HiveType hive)
        {
            var containerName = GetContainerName(hive);
            return _cloudBlobClient.GetContainerReference(containerName);
        }
    }
}

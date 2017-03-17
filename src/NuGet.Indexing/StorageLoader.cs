// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using System;
using System.IO;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;
using Microsoft.WindowsAzure.Storage.Blob;

namespace NuGet.Indexing
{
    public class StorageLoader : ILoader
    {
        private readonly FrameworkLogger _logger;

        private string _dataContainerName;
        private string _storageAccountConnectionString;
        private CloudBlobClient _client;
        private CloudBlobContainer _container;

        public StorageLoader(IndexingConfiguration config, FrameworkLogger logger)
        {
            _logger = logger;
            Reload(config);
        }

        public JsonReader GetReader(string blobName)
        {
            try
            {
                _logger.LogInformation($"{nameof(StorageLoader)}.${nameof(GetReader)}: {blobName}");

                var blob = GetBlob(blobName);
                return new JsonTextReader(new StreamReader(blob.OpenRead()));
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception {e.Message} attempting to load {blobName}", e);
                throw;
            }
        }

        public DateTime? GetLastUpdateTime(string blobName)
        {
            try
            {
                _logger.LogInformation($"{nameof(StorageLoader)}.{nameof(GetLastUpdateTime)}: {blobName}");

                var blob = GetBlob(blobName);
                if (!blob.Exists())
                {
                    return null;
                }

                blob.FetchAttributes();

                // LastModified time is always in UTC for Azure blobs
                DateTimeOffset? lastModifiedUtcTime = blob.Properties.LastModified;

                // However, the blobs' LastModified time's UTC locale isn't specified in the object, hence, explicitly set kind to UTC zone.
                return lastModifiedUtcTime.HasValue
                    ? new DateTime(lastModifiedUtcTime.Value.Ticks, DateTimeKind.Utc)
                    : (DateTime?)null;
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception {e.Message} attempting to get metadata for blob {blobName}", e);
                throw;
            }
        }

        public bool Reload(IndexingConfiguration config)
        {
            // Refresh the data container and the primary storage account.
            var oldDataContainerName = _dataContainerName;
            _dataContainerName = config.DataContainer;

            var oldStorageAccountConnectionString = _storageAccountConnectionString;
            _storageAccountConnectionString = config.StoragePrimary;
            var storageAccount = CloudStorageAccount.Parse(_storageAccountConnectionString);
            _client = storageAccount.CreateCloudBlobClient();
            _container = _client.GetContainerReference(_dataContainerName);

            _logger.LogInformation("StorageLoader data container: {DataContainerName}", _dataContainerName);

            // Our data has changed if the data container name or storage account connection string has changed.
            return
                !(oldDataContainerName == _dataContainerName &&
                  oldStorageAccountConnectionString == _storageAccountConnectionString);
        }

        private CloudBlockBlob GetBlob(string blobName)
        {
            return _container.GetBlockBlobReference(blobName);
        }
    }
}

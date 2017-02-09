// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using NuGet.Services.Configuration;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public class StorageLoader : ILoader
    {
        private readonly FrameworkLogger _logger;

        private string _dataContainerName;
        private string _storageAccountConnectionString;
        private CloudStorageAccount _storageAccount;

        public StorageLoader(IndexingConfiguration config, FrameworkLogger logger)
        {
            _logger = logger;
            Reload(config);
        }

        public JsonReader GetReader(string name)
        {
            try
            {
                _logger.LogInformation("StorageLoader.GetReader: {ReaderTarget}", name);

                var client = _storageAccount.CreateCloudBlobClient();
                var container = client.GetContainerReference(_dataContainerName);
                var blob = container.GetBlockBlobReference(name);
                return new JsonTextReader(new StreamReader(blob.OpenRead()));
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception {e.Message} attempting to load {name}", e);
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
            _storageAccount = CloudStorageAccount.Parse(_storageAccountConnectionString);

            _logger.LogInformation("StorageLoader data container: {DataContainerName}", _dataContainerName);

            // Our data has changed if the data container name or storage account connection string has changed.
            return
                !(oldDataContainerName == _dataContainerName &&
                  oldStorageAccountConnectionString == _storageAccountConnectionString);
        }
    }
}

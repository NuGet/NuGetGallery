// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using Lucene.Net.Store;
using Lucene.Net.Store.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing.IndexDirectoryProvider
{
    using IODirectory = System.IO.Directory;
    using LuceneDirectory = Lucene.Net.Store.Directory;

    /// <summary>
    /// Maintains an index on the cloud. Provides a synchronizer and a reload method to refresh the index.
    /// </summary>
    public class CloudIndexDirectoryProvider : IIndexDirectoryProvider
    {
        private readonly FrameworkLogger _logger;

        private LuceneDirectory _directory;
        private string _indexContainerName;
        private string _storageAccountConnectionString;
        private AzureDirectorySynchronizer _synchronizer;

        public CloudIndexDirectoryProvider(IndexingConfiguration config, FrameworkLogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Reload(config);
        }

        public LuceneDirectory GetDirectory()
        {
            return _directory;
        }

        public string GetIndexContainerName()
        {
            return _indexContainerName;
        }

        public AzureDirectorySynchronizer GetSynchronizer()
        {
            return _synchronizer;
        }

        public bool Reload(IndexingConfiguration config)
        {
            // If we have a directory and the index container has not changed, we don't need to reload.
            // We don't want to reload the index unless necessary.
            var newStorageAccountConnectionString = config.StoragePrimary;
            var newIndexContainerName = config.IndexContainer;
            if (_directory != null && 
                newStorageAccountConnectionString == _storageAccountConnectionString &&
                newIndexContainerName == _indexContainerName)
            {
                return false;
            }
            
            _storageAccountConnectionString = newStorageAccountConnectionString;
            _indexContainerName = newIndexContainerName;

            _logger.LogInformation("Recognized index configuration change.");

            var storageAccount = CloudStorageAccount.Parse(_storageAccountConnectionString);

            _logger.LogInformation(
                "Reloading index with new settings. Storage Account Name = {StorageAccountName}, Container = {IndexContainerName}",
                storageAccount, _indexContainerName);

            var stopwatch = Stopwatch.StartNew();

            AzureDirectory sourceDirectory;

            if (string.IsNullOrEmpty(config.AzureDirectoryCachePath))
            {
                sourceDirectory = new AzureDirectory(storageAccount, _indexContainerName);
            }
            else
            {
                var azureDirectoryCachePath = Path.GetFullPath(Path.Combine(config.AzureDirectoryCachePath, _indexContainerName));

                _logger.LogInformation("Using custom Azure Directory cache path: {AzureDirectoryCachePath}", azureDirectoryCachePath);

                IODirectory.CreateDirectory(azureDirectoryCachePath);

                var cacheDirectory = FSDirectory.Open(azureDirectoryCachePath);
                sourceDirectory = new AzureDirectory(storageAccount, _indexContainerName, cacheDirectory);
            }

            _directory = new RAMDirectory(sourceDirectory); // Copy the directory from Azure storage to RAM.

            _synchronizer = new AzureDirectorySynchronizer(sourceDirectory, _directory);

            stopwatch.Stop();
            _logger.LogInformation($"Index reload completed and took {stopwatch.Elapsed.Seconds} seconds.");

            return true;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGetGallery;

namespace NuGet.Services.AzureSearch
{
    public class BlobContainerBuilder : IBlobContainerBuilder
    {
        private readonly ICloudBlobClient _cloudBlobClient;
        private readonly IOptionsSnapshot<AzureSearchConfiguration> _options;
        private readonly ILogger<BlobContainerBuilder> _logger;
        private readonly Lazy<ICloudBlobContainer> _lazyContainer;
        private readonly TimeSpan _retryDelay;

        public BlobContainerBuilder(
            ICloudBlobClient cloudBlobClient,
            IOptionsSnapshot<AzureSearchConfiguration> options,
            ILogger<BlobContainerBuilder> logger) : this(
                cloudBlobClient,
                options,
                logger,
                retryDelay: TimeSpan.FromSeconds(10))
        {
        }

        /// <summary>
        /// This constructor is used for testing.
        /// </summary>
        internal BlobContainerBuilder(
            ICloudBlobClient cloudBlobClient,
            IOptionsSnapshot<AzureSearchConfiguration> options,
            ILogger<BlobContainerBuilder> logger,
            TimeSpan retryDelay)
        {
            _cloudBlobClient = cloudBlobClient ?? throw new ArgumentNullException(nameof(cloudBlobClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lazyContainer = new Lazy<ICloudBlobContainer>(() =>
            {
                return _cloudBlobClient.GetContainerReference(_options.Value.StorageContainer);
            });
            _retryDelay = retryDelay;
        }

        private ICloudBlobContainer Container => _lazyContainer.Value;

        public async Task CreateAsync(bool retryOnConflict)
        {
            _logger.LogInformation("Creating blob container {ContainerName}.", _options.Value.StorageContainer);
            var containerCreated = false;
            var waitStopwatch = Stopwatch.StartNew();
            while (!containerCreated)
            {
                try
                {
                    await Container.CreateAsync();
                    await Container.SetPermissionsAsync(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });
                    containerCreated = true;
                }
                catch (StorageException ex) when (retryOnConflict && ex.RequestInformation.HttpStatusCode == (int)HttpStatusCode.Conflict)
                {
                    if (waitStopwatch.Elapsed < TimeSpan.FromMinutes(5))
                    {
                        _logger.LogInformation(
                            "The blob container is still being deleted. Attempting creation again in {RetryDelay}.",
                            _retryDelay);
                        await Task.Delay(_retryDelay);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            _logger.LogInformation("Done creating blob container {ContainerName}.", _options.Value.StorageContainer);
        }

        public async Task CreateIfNotExistsAsync()
        {
            if (await Container.ExistsAsync(null, null))
            {
                _logger.LogInformation("Skipping creation of blob container {ContainerName} since it already exists.", _options.Value.StorageContainer);
            }
            else
            {
                await CreateAsync(retryOnConflict: false);
            }
        }

        public async Task<bool> DeleteIfExistsAsync()
        {
            _logger.LogWarning("Attempting to delete blob container {ContainerName}.", _options.Value.StorageContainer);
            var containerDeleted = await Container.DeleteIfExistsAsync();
            if (containerDeleted)
            {
                _logger.LogWarning("Done deleting blob container {ContainerName}.", _options.Value.StorageContainer);
            }
            else
            {
                _logger.LogInformation("Blob container {ContainerName} was not deleted since it does not exist.", _options.Value.StorageContainer);
            }

            return containerDeleted;
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.V3PerPackage
{
    public class CleanUpCommand
    {
        private readonly GlobalContext _globalContext;
        private readonly ILogger<CleanUpCommand> _logger;

        public CleanUpCommand(GlobalContext globalContext, ILogger<CleanUpCommand> logger)
        {
            _globalContext = globalContext;
            _logger = logger;
        }

        public async Task ExecuteAsync()
        {
            var blobClient = BlobStorageUtilities.GetBlobClient(_globalContext);

            // Clean up catalog2lucene containers.
            var luceneContainers = blobClient.ListContainers("v3-lucene-");
            foreach (var container in luceneContainers)
            {
                await CleanUpUtilities.DeleteContainer(blobClient, container.Name, _logger);
            }

            var luceneCachePath = CleanUpUtilities.GetLuceneCacheDirectory();
            foreach (var directory in Directory.EnumerateDirectories(luceneCachePath, "v3-lucene-"))
            {
                Directory.Delete(directory, recursive: true);
            }

            await CleanUpUtilities.DeleteContainer(blobClient, _globalContext.RegistrationContainerName, _logger);
            await CleanUpUtilities.DeleteContainer(blobClient, _globalContext.FlatContainerContainerName, _logger);
            await CleanUpUtilities.DeleteContainer(blobClient, _globalContext.CatalogContainerName, _logger);
        }
    }
}

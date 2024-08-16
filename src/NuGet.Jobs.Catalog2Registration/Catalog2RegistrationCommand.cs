// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.V3;
using NuGetGallery;

namespace NuGet.Jobs.Catalog2Registration
{
    public class Catalog2RegistrationCommand
    {
        public const string CursorRelativeUri = "cursor.json";

        private readonly ICollector _collector;
        private readonly ICloudBlobClient _cloudBlobClient;
        private readonly IStorageFactory _storageFactory;
        private readonly Func<HttpMessageHandler> _handlerFunc;
        private readonly IOptionsSnapshot<Catalog2RegistrationConfiguration> _options;
        private readonly ILogger<Catalog2RegistrationCommand> _logger;

        public Catalog2RegistrationCommand(
            ICollector collector,
            ICloudBlobClient cloudBlobClient,
            IStorageFactory storageFactory,
            Func<HttpMessageHandler> handlerFunc,
            IOptionsSnapshot<Catalog2RegistrationConfiguration> options,
            ILogger<Catalog2RegistrationCommand> logger)
        {
            _collector = collector ?? throw new ArgumentNullException(nameof(collector));
            _cloudBlobClient = cloudBlobClient ?? throw new ArgumentNullException(nameof(cloudBlobClient));
            _storageFactory = storageFactory ?? throw new ArgumentNullException(nameof(storageFactory));
            _handlerFunc = handlerFunc ?? throw new ArgumentNullException(nameof(handlerFunc));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExecuteAsync()
        {
            await ExecuteAsync(CancellationToken.None);
        }

        private async Task ExecuteAsync(CancellationToken token)
        {
            // Initialize the cursors.
            ReadCursor backCursor;
            if (_options.Value.DependencyCursorUrls != null
                && _options.Value.DependencyCursorUrls.Any())
            {
                _logger.LogInformation("Depending on cursors: {DependencyCursorUrls}", _options.Value.DependencyCursorUrls);
                backCursor = new AggregateCursor(_options
                    .Value
                    .DependencyCursorUrls.Select(r => new HttpReadCursor(new Uri(r), _handlerFunc)));
            }
            else
            {
                _logger.LogInformation("Depending on no cursors, meaning the job will process up to the latest catalog information.");
                backCursor = MemoryCursor.CreateMax();
            }

            var frontCursorStorage = _storageFactory.Create();
            var frontCursorUri = frontCursorStorage.ResolveUri(CursorRelativeUri);
            var frontCursor = new DurableCursor(frontCursorUri, frontCursorStorage, DateTime.MinValue);

            _logger.LogInformation("Using cursor: {CursurUrl}", frontCursorUri.AbsoluteUri);
            LogContainerUrl(HiveType.Legacy, c => c.LegacyStorageContainer);
            LogContainerUrl(HiveType.Gzipped, c => c.GzippedStorageContainer);
            LogContainerUrl(HiveType.SemVer2, c => c.SemVer2StorageContainer);

            // Optionally create the containers.
            if (_options.Value.CreateContainers)
            {
                await CreateContainerIfNotExistsAsync(c => c.LegacyStorageContainer);
                await CreateContainerIfNotExistsAsync(c => c.GzippedStorageContainer);
                await CreateContainerIfNotExistsAsync(c => c.SemVer2StorageContainer);
            }

            await frontCursor.LoadAsync(token);
            await backCursor.LoadAsync(token);
            _logger.LogInformation(
                "The cursors have been loaded. Front: {FrontCursor}. Back: {BackCursor}.",
                frontCursor.Value,
                backCursor.Value);

            // Run the collector.
            await _collector.RunAsync(
                frontCursor,
                backCursor,
                token);
        }

        private async Task CreateContainerIfNotExistsAsync(Func<Catalog2RegistrationConfiguration, string> getContainer)
        {
            var containerName = getContainer(_options.Value);
            var container = _cloudBlobClient.GetContainerReference(containerName);

            _logger.LogInformation("Creating container {Container} with blobs publicly available if it does not already exist.", containerName);
            await container.CreateIfNotExistAsync(enablePublicAccess: true);
        }

        private void LogContainerUrl(HiveType hive, Func<Catalog2RegistrationConfiguration, string> getContainer)
        {
            _logger.LogInformation(
                "Using {Hive} storage: {ContainerUrl}",
                hive,
                CloudStorageAccount.Parse(_options.Value.StorageConnectionString)
                    .CreateCloudBlobClient()
                    .GetContainerReference(getContainer(_options.Value))
                    .Uri
                    .AbsoluteUri);
        }
    }
}

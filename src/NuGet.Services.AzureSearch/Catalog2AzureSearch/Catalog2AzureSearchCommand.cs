// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.V3;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class Catalog2AzureSearchCommand : IAzureSearchCommand
    {
        public const string CursorRelativeUri = "cursor.json";

        private readonly ICollector _collector;
        private readonly IStorageFactory _storageFactory;
        private readonly Func<HttpMessageHandler> _handlerFunc;
        private readonly IBlobContainerBuilder _blobContainerBuilder;
        private readonly IIndexBuilder _indexBuilder;
        private readonly IOptionsSnapshot<Catalog2AzureSearchConfiguration> _options;
        private readonly ILogger<Catalog2AzureSearchCommand> _logger;

        public Catalog2AzureSearchCommand(
            ICollector collector,
            IStorageFactory storageFactory,
            Func<HttpMessageHandler> handlerFunc,
            IBlobContainerBuilder blobContainerBuilder,
            IIndexBuilder indexBuilder,
            IOptionsSnapshot<Catalog2AzureSearchConfiguration> options,
            ILogger<Catalog2AzureSearchCommand> logger)
        {
            _collector = collector ?? throw new ArgumentNullException(nameof(collector));
            _storageFactory = storageFactory ?? throw new ArgumentNullException(nameof(storageFactory));
            _handlerFunc = handlerFunc ?? throw new ArgumentNullException(nameof(handlerFunc));
            _blobContainerBuilder = blobContainerBuilder ?? throw new ArgumentNullException(nameof(blobContainerBuilder));
            _indexBuilder = indexBuilder ?? throw new ArgumentNullException(nameof(indexBuilder));
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
                _logger.LogInformation("Depending on cursors:{DependencyCursorUrls}", _options.Value.DependencyCursorUrls);
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

            // workaround for https://github.com/Azure/azure-sdk-for-net/issues/44373
            var connectionString = _options.Value.StorageConnectionString.Replace("SharedAccessSignature=?", "SharedAccessSignature=");

            // Log information about where state will be kept.
            _logger.LogInformation(
                "Using storage URL: {ContainerUrl}/{StoragePath}",
                new BlobServiceClient(connectionString)
                    .GetBlobContainerClient(_options.Value.StorageContainer)
                    .Uri
                    .AbsoluteUri,
                _options.Value.NormalizeStoragePath());
            _logger.LogInformation("Using cursor: {CursurUrl}", frontCursorUri.AbsoluteUri);
            _logger.LogInformation("Using search service: {SearchServiceName}", _options.Value.SearchServiceName);
            _logger.LogInformation("Using search index: {IndexName}", _options.Value.SearchIndexName);
            _logger.LogInformation("Using hijack index: {IndexName}", _options.Value.HijackIndexName);

            // Optionally create the indexes.
            if (_options.Value.CreateContainersAndIndexes)
            {
                await _blobContainerBuilder.CreateIfNotExistsAsync();
                await _indexBuilder.CreateSearchIndexIfNotExistsAsync();
                await _indexBuilder.CreateHijackIndexIfNotExistsAsync();
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
    }
}

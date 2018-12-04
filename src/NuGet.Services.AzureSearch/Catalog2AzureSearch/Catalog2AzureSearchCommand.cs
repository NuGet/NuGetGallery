// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class Catalog2AzureSearchCommand
    {
        private readonly ICollector _collector;
        private readonly IStorageFactory _storageFactory;
        private readonly Func<HttpMessageHandler> _handlerFunc;
        private readonly IIndexBuilder _indexBuilder;
        private readonly IOptionsSnapshot<Catalog2AzureSearchConfiguration> _options;
        private readonly ILogger<Catalog2AzureSearchCommand> _logger;

        public Catalog2AzureSearchCommand(
            ICollector collector,
            IStorageFactory storageFactory,
            Func<HttpMessageHandler> handlerFunc,
            IIndexBuilder indexBuilder,
            IOptionsSnapshot<Catalog2AzureSearchConfiguration> options,
            ILogger<Catalog2AzureSearchCommand> logger)
        {
            _collector = collector ?? throw new ArgumentNullException(nameof(collector));
            _storageFactory = storageFactory ?? throw new ArgumentNullException(nameof(storageFactory));
            _handlerFunc = handlerFunc ?? throw new ArgumentNullException(nameof(handlerFunc));
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
            // Optionally create the indexes.
            if (_options.Value.CreateIndexes)
            {
                await _indexBuilder.CreateSearchIndexIfNotExistsAsync();
                await _indexBuilder.CreateHijackIndexIfNotExistsAsync();
            }

            // Initialize the cursors.
            var frontCursorStorage = _storageFactory.Create();
            var frontCursor = new DurableCursor(
                frontCursorStorage.ResolveUri("cursor.json"),
                frontCursorStorage,
                DateTime.MinValue);

            ReadCursor backCursor;
            if (_options.Value.DependencyCursorUrls != null
                && _options.Value.DependencyCursorUrls.Any())
            {
                backCursor = new AggregateCursor(_options
                    .Value
                    .DependencyCursorUrls.Select(r => new HttpReadCursor(new Uri(r), _handlerFunc)));
            }
            else
            {
                backCursor = MemoryCursor.CreateMax();
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

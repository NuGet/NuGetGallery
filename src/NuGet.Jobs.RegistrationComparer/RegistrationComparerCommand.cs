// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.V3;

namespace NuGet.Jobs.RegistrationComparer
{
    public class RegistrationComparerCommand
    {
        private readonly ICollector _collector;
        private readonly CloudStorageAccount _storageAccount;
        private readonly IStorageFactory _storageFactory;
        private readonly Func<HttpMessageHandler> _handlerFunc;
        private readonly IOptionsSnapshot<RegistrationComparerConfiguration> _options;
        private readonly ILogger<RegistrationComparerCommand> _logger;

        public RegistrationComparerCommand(
            ICollector collector,
            CloudStorageAccount storageAccount,
            IStorageFactory storageFactory,
            Func<HttpMessageHandler> handlerFunc,
            IOptionsSnapshot<RegistrationComparerConfiguration> options,
            ILogger<RegistrationComparerCommand> logger)
        {
            _collector = collector ?? throw new ArgumentNullException(nameof(collector));
            _storageAccount = storageAccount ?? throw new ArgumentNullException(nameof(storageAccount));
            _storageFactory = storageFactory ?? throw new ArgumentNullException(nameof(storageFactory));
            _handlerFunc = handlerFunc ?? throw new ArgumentNullException(nameof(handlerFunc));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var registrationCursors = CursorUtility.GetRegistrationCursors(_handlerFunc, _options);
            var backCursor = new AggregateCursor(registrationCursors.Values);

            var frontCursorPair = CursorUtility.GetComparerCursor(_storageFactory);
            _logger.LogInformation("Using cursor: {CursurUrl}", frontCursorPair.Key);
            var frontCursor = frontCursorPair.Value;

            await _storageAccount
                .CreateCloudBlobClient()
                .GetContainerReference(_options.Value.StorageContainer)
                .CreateIfNotExistsAsync();

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

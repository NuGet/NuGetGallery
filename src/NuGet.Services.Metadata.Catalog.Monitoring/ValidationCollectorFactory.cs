// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Storage;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Assists with initializing a <see cref="ValidationCollector"/>.
    /// </summary>
    public class ValidationCollectorFactory
    {
        private ILoggerFactory _loggerFactory;
        private ILogger<ValidationCollectorFactory> _logger;

        /// <summary>
        /// Context object returned by <see cref="Create(IStorageQueue{PackageValidatorContext}, string, Persistence.IStorageFactory, IEnumerable{EndpointFactory.Input}, Func{HttpMessageHandler})"/>.
        /// Contains a <see cref="ValidationCollector"/> and two cursors to use with it.
        /// </summary>
        public class Result
        {
            public Result(ValidationCollector collector, ReadWriteCursor front, ReadCursor back)
            {
                Collector = collector;
                Front = front;
                Back = back;
            }

            public ValidationCollector Collector { get; }
            public ReadWriteCursor Front { get; }
            public ReadCursor Back { get; }
        }

        public ValidationCollectorFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<ValidationCollectorFactory>();
        }

        /// <summary>
        /// Constructs a <see cref="ValidationCollector"/> from inputs and returns a <see cref="Result"/>.
        /// </summary>
        /// <param name="queue">Queue that the <see cref="ValidationCollector"/> queues packages to.</param>
        /// <param name="catalogIndexUrl">Url of the catalog that the <see cref="ValidationCollector"/> should run on.</param>
        /// <param name="monitoringStorageFactory">Storage where the cursors used by the <see cref="ValidationCollector"/> are stored.</param>
        /// <param name="endpointInputs">Endpoints that validations will be run on for queued packages.</param>
        /// <param name="messageHandlerFactory">Used by <see cref="ValidationCollector"/> to construct a <see cref="CollectorHttpClient"/>.</param>
        public Result Create(
            IStorageQueue<PackageValidatorContext> queue,
            string catalogIndexUrl,
            Persistence.IStorageFactory monitoringStorageFactory,
            IEnumerable<EndpointFactory.Input> endpointInputs,
            Func<HttpMessageHandler> messageHandlerFactory)
        {
            var collector = new ValidationCollector(
                queue,
                new Uri(catalogIndexUrl),
                _loggerFactory.CreateLogger<ValidationCollector>(),
                messageHandlerFactory);

            var front = GetFront(monitoringStorageFactory);
            var back = new AggregateCursor(endpointInputs.Select(input => new HttpReadCursor(input.CursorUri, messageHandlerFactory)));

            return new Result(collector, front, back);
        }

        public static DurableCursor GetFront(Persistence.IStorageFactory storageFactory)
        {
            var storage = storageFactory.Create();
            return new DurableCursor(storage.ResolveUri("cursor.json"), storage, MemoryCursor.MinValue);
        }
    }
}
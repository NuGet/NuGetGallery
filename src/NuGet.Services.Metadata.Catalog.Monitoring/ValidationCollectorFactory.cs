// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Assists with initializing a <see cref="ValidationCollector"/>.
    /// </summary>
    public class ValidationCollectorFactory
    {
        private ILoggerFactory _loggerFactory;
        private ILogger<ValidationCollectorFactory> _logger;

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

        public Result Create(
            string galleryUrl,
            string indexUrl,
            string catalogIndexUrl,
            StorageFactory cursorStorage,
            StorageFactory auditingStorageFactory,
            IEnumerable<EndpointFactory.Input> endpointContexts,
            Func<HttpMessageHandler> messageHandlerFactory,
            IMonitoringNotificationService notificationService,
            bool verbose = false)
        {
            _logger.LogInformation(
                "CONFIG gallery: {Gallery} index: {Index} source: {ConfigSource} storage: {Storage} auditingStorage: {AuditingStorage} endpoints: {Endpoints}",
                galleryUrl, indexUrl, catalogIndexUrl, cursorStorage, auditingStorageFactory, string.Join(", ", endpointContexts.Select(e => e.Name)));

            var validationFactory = new ValidatorFactory(new Dictionary<FeedType, SourceRepository>()
            {
                {FeedType.HttpV2, new SourceRepository(new PackageSource(galleryUrl), GetResourceProviders(ResourceProvidersToInjectV2), FeedType.HttpV2)},
                {FeedType.HttpV3, new SourceRepository(new PackageSource(indexUrl), GetResourceProviders(ResourceProvidersToInjectV3), FeedType.HttpV3)}
            }, _loggerFactory);

            var endpointFactory = new EndpointFactory(
                validationFactory,
                messageHandlerFactory,
                _loggerFactory);

            var endpoints = endpointContexts.Select(e => endpointFactory.Create(e));

            var collector = new ValidationCollector(auditingStorageFactory.Create(),
                new PackageValidator(endpoints, _loggerFactory.CreateLogger<PackageValidator>()),
                new Uri(catalogIndexUrl),
                notificationService,
                messageHandlerFactory);

            var storage = cursorStorage.Create();
            var front = new DurableCursor(storage.ResolveUri("cursor.json"), storage, MemoryCursor.MinValue);
            var back = new AggregateCursor(endpoints.Select(e => e.Cursor));

            return new Result(collector, front, back);
        }

        private IList<Lazy<INuGetResourceProvider>> ResourceProvidersToInjectV2 => new List<Lazy<INuGetResourceProvider>>
        {
            new Lazy<INuGetResourceProvider>(() => new NonhijackableV2HttpHandlerResourceProvider()),
            new Lazy<INuGetResourceProvider>(() => new PackageTimestampMetadataResourceV2Provider(_loggerFactory)),
            new Lazy<INuGetResourceProvider>(() => new PackageRegistrationMetadataResourceV2FeedProvider())
        };

        private IList<Lazy<INuGetResourceProvider>> ResourceProvidersToInjectV3 => new List<Lazy<INuGetResourceProvider>>
        {
            new Lazy<INuGetResourceProvider>(() => new PackageRegistrationMetadataResourceV3Provider())
        };

        private IEnumerable<Lazy<INuGetResourceProvider>> GetResourceProviders(IList<Lazy<INuGetResourceProvider>> providersToInject)
        {
            var resourceProviders = Repository.Provider.GetCoreV3().ToList();
            resourceProviders.AddRange(providersToInject);
            return resourceProviders;
        }
    }
}

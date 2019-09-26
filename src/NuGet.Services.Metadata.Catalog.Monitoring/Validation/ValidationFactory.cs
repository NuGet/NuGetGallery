// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Storage;
using StorageFactory = NuGet.Services.Metadata.Catalog.Persistence.StorageFactory;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// Helper class for constructing validation business logic implementations.
    /// </summary>
    public static class ValidationFactory
    {
        public static PackageValidator CreatePackageValidator(
            string galleryUrl,
            string indexUrl,
            StorageFactory auditingStorageFactory,
            ValidatorConfiguration validatorConfig,
            EndpointConfiguration endpointConfig,
            Func<HttpMessageHandler> messageHandlerFactory,
            IGalleryDatabaseQueryService galleryDatabase,
            ILoggerFactory loggerFactory)
        {
            if (auditingStorageFactory == null)
            {
                throw new ArgumentNullException(nameof(auditingStorageFactory));
            }

            var collection = new ServiceCollection();
            collection.AddSingleton(loggerFactory);
            collection.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            var builder = new ContainerBuilder();
            builder.Populate(collection);
            
            builder.RegisterValidatorConfiguration(validatorConfig);
            builder.RegisterEndpointConfiguration(endpointConfig);
            builder.RegisterMessageHandlerFactory(messageHandlerFactory);
            builder.RegisterEndpoints(endpointConfig);
            builder.RegisterSourceRepositories(galleryUrl, indexUrl, galleryDatabase);
            builder.RegisterValidators(endpointConfig);

            builder
                .RegisterInstance(auditingStorageFactory)
                .AsSelf()
                .As<StorageFactory>();

            builder.RegisterType<PackageValidator>();

            var container = builder.Build();

            return container.Resolve<PackageValidator>();
        }

        public static PackageValidatorContextEnqueuer CreatePackageValidatorContextEnqueuer(
            IStorageQueue<PackageValidatorContext> queue,
            string catalogIndexUrl,
            Persistence.IStorageFactory monitoringStorageFactory,
            EndpointConfiguration endpointConfig,
            ITelemetryService telemetryService,
            Func<HttpMessageHandler> messageHandlerFactory,
            ILoggerFactory loggerFactory)
        {
            if (queue == null)
            {
                throw new ArgumentNullException(nameof(queue));
            }

            if (string.IsNullOrEmpty(catalogIndexUrl))
            {
                throw new ArgumentException(nameof(catalogIndexUrl));
            }

            if (monitoringStorageFactory == null)
            {
                throw new ArgumentNullException(nameof(monitoringStorageFactory));
            }

            if (telemetryService == null)
            {
                throw new ArgumentNullException(nameof(telemetryService));
            }

            var collection = new ServiceCollection();
            collection.AddSingleton(loggerFactory);
            collection.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            var builder = new ContainerBuilder();
            builder.Populate(collection);

            builder.RegisterEndpointConfiguration(endpointConfig);
            builder.RegisterEndpoints(endpointConfig);
            builder.RegisterMessageHandlerFactory(messageHandlerFactory);

            builder
                .RegisterInstance(queue)
                .As<IStorageQueue<PackageValidatorContext>>();

            builder
                .RegisterInstance(new Uri(catalogIndexUrl))
                .As<Uri>();

            builder
                .RegisterInstance(telemetryService)
                .As<ITelemetryService>();

            builder
                .RegisterType<ValidationCollector>()
                .As<ValidationCollector>();

            builder
                .RegisterInstance(GetFront(monitoringStorageFactory))
                .As<ReadWriteCursor>();

            builder
                .RegisterType<AggregateEndpointCursor>()
                .As<ReadCursor>();

            builder
                .RegisterType<PackageValidatorContextEnqueuer>()
                .As<PackageValidatorContextEnqueuer>();

            var container = builder.Build();

            return container.Resolve<PackageValidatorContextEnqueuer>();
        }

        public static DurableCursor GetFront(Persistence.IStorageFactory storageFactory)
        {
            var storage = storageFactory.Create();
            return new DurableCursor(storage.ResolveUri("cursor.json"), storage, MemoryCursor.MinValue);
        }
    }
}

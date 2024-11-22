// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Autofac;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Protocol;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.Storage;
using NuGet.Services.V3;
using NuGetGallery;

using AzureStorage = NuGet.Services.Metadata.Catalog.Persistence.AzureStorage;
using AzureStorageFactory = NuGet.Services.Metadata.Catalog.Persistence.AzureStorageFactory;
using IStorageFactory = NuGet.Services.Metadata.Catalog.Persistence.IStorageFactory;

namespace NuGet.Jobs.Catalog2Registration
{
    public static class DependencyInjectionExtensions
    {
        public const string CursorBindingKey = "Cursor";

        public static ContainerBuilder AddCatalog2Registration(this ContainerBuilder containerBuilder)
        {
            containerBuilder.AddV3();

            RegisterCursorStorage(containerBuilder);
            containerBuilder
                .Register<ICloudBlobClient>(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();

                    if (options.Value.StorageUseManagedIdentity && !options.Value.HasSasToken)
                    {
                        return CloudBlobClientWrapper.UsingMsi(options.Value.StorageConnectionString, clientId: options.Value.StorageManagedIdentityClientId, requestTimeout: DefaultBlobRequestOptions.ServerTimeout);
                    }

                    return new CloudBlobClientWrapper(
                        options.Value.StorageConnectionString,
                        requestTimeout: DefaultBlobRequestOptions.ServerTimeout);
                });

            containerBuilder.Register(c => new Catalog2RegistrationCommand(
                c.Resolve<ICollector>(),
                c.Resolve<ICloudBlobClient>(),
                c.ResolveKeyed<IStorageFactory>(CursorBindingKey),
                c.Resolve<Func<HttpMessageHandler>>(),
                c.Resolve<IOptionsSnapshot<Catalog2RegistrationConfiguration>>(),
                c.Resolve<ILogger<Catalog2RegistrationCommand>>()));

            return containerBuilder;
        }

        private static void RegisterCursorStorage(ContainerBuilder containerBuilder)
        {
            // Register NuGet.Services.Metadata storage abstractions with a binding key so that they are not as easy to
            // consume. This is an intentional decision since product code should use the NuGetGallery.Core storage
            // abstractions (e.g. ICloudBlobClient).
            containerBuilder
                .Register(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();

                    if (options.Value.StorageUseManagedIdentity && !options.Value.HasSasToken)
                    {
                        var credential = new ManagedIdentityCredential(options.Value.StorageManagedIdentityClientId);

                        return new BlobServiceClientFactory(new Uri(options.Value.StorageServiceUrl), credential);
                    }
                    var connectionString = options.Value.StorageConnectionString.Replace("SharedAccessSignature=?", "SharedAccessSignature=");

                    return new BlobServiceClientFactory(connectionString);
                })
                .Keyed<IBlobServiceClientFactory>(CursorBindingKey);

            containerBuilder
                .Register<IStorageFactory>(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();

                    return new AzureStorageFactory(
                        c.ResolveKeyed<IBlobServiceClientFactory>(CursorBindingKey),
                        options.Value.LegacyStorageContainer,
                        maxExecutionTime: AzureStorage.DefaultMaxExecutionTime,
                        serverTimeout: AzureStorage.DefaultServerTimeout,
                        path: string.Empty,
                        baseAddress: null,
                        useServerSideCopy: true,
                        compressContent: false,
                        verbose: true,
                        initializeContainer: false,
                        throttle: NullThrottle.Instance);
                })
                .Keyed<IStorageFactory>(CursorBindingKey);
        }

        public static IServiceCollection AddCatalog2Registration(
            this IServiceCollection services,
            IDictionary<string, string> telemetryGlobalDimensions,
            IConfigurationRoot configurationRoot)
        {
            services.AddV3(telemetryGlobalDimensions, configurationRoot);

            services.AddTransient<ICommitCollectorLogic, RegistrationCollectorLogic>();
            services.AddTransient<IHiveMerger, HiveMerger>();
            services.AddTransient<IHiveStorage, HiveStorage>();
            services.AddTransient<IHiveUpdater, HiveUpdater>();
            services.AddTransient<IEntityBuilder, EntityBuilder>();
            services.AddTransient<IRegistrationUpdater, RegistrationUpdater>();
            services.AddTransient<RegistrationUrlBuilder>();

            services.AddSingleton<IThrottle>(s =>
            {
                var config = s.GetRequiredService<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();
                return SemaphoreSlimThrottle.CreateSemaphoreThrottle(config.Value.MaxConcurrentStorageOperations);
            });

            return services;
        }
    }
}

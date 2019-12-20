// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Protocol;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Services.V3;
using NuGetGallery;

namespace NuGet.Jobs.Catalog2Registration
{
    public static class DependencyInjectionExtensions
    {
        private const string CursorBindingKey = "Cursor";

        public static ContainerBuilder AddCatalog2Registration(this ContainerBuilder containerBuilder)
        {
            RegisterCursorStorage(containerBuilder);

            containerBuilder
                .Register<ICloudBlobClient>(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();
                    return new CloudBlobClientWrapper(
                        options.Value.StorageConnectionString,
                        DefaultBlobRequestOptions.Create());
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
                    return CloudStorageAccount.Parse(options.Value.StorageConnectionString);
                })
                .Keyed<CloudStorageAccount>(CursorBindingKey);

            containerBuilder
                .Register<IStorageFactory>(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<Catalog2RegistrationConfiguration>>();
                    return new AzureStorageFactory(
                        c.ResolveKeyed<CloudStorageAccount>(CursorBindingKey),
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

        public static IServiceCollection AddCatalog2Registration(this IServiceCollection services)
        {
            services.AddV3();

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

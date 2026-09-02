// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Autofac.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGetGallery;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Hosts staged package promotion message processing.
    /// </summary>
    public class Job : SubscriptionProcessorJob<StagedPackagePromotionMessage>
    {
        private const string PromotionConfigurationSectionName = "Promotion";
        private const string PackageStorageKey = "PackageStorage";
        private const string StagingStorageKey = "StagingStorage";
        private const string FlatContainerStorageKey = "FlatContainerStorage";

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<PromotionConfiguration>(configurationRoot.GetSection(PromotionConfigurationSectionName));
            SetupDefaultSubscriptionProcessorConfiguration(services, configurationRoot);
            services.Configure<SubscriptionProcessorConfiguration>(configuration => configuration.MaxConcurrentCalls = 1);

            services.AddScoped<IEntitiesContext>(serviceProvider =>
            {
                var connectionFactory = serviceProvider.GetRequiredService<ISqlConnectionFactory<GalleryDbConfiguration>>();
                var connection = connectionFactory.CreateAsync().GetAwaiter().GetResult();
                return new EntitiesContext(connection, readOnly: false);
            });
            services.Add(ServiceDescriptor.Transient(typeof(IEntityRepository<>), typeof(EntityRepository<>)));
            services.AddTransient<ICorePackageService, CorePackageService>();
            services.AddTransient<IFileMetadataService, PackageFileMetadataService>();
            services.AddTransient<IBrokeredMessageSerializer<StagedPackagePromotionMessage>, StagedPackagePromotionMessageSerializer>();
            services.AddTransient<ISubscriptionProcessorTelemetryService, SubscriptionProcessorNoTelemetryService>();
            services.AddTransient<ICloudBlobContainerInformationProvider, GalleryCloudBlobContainerInformationProvider>();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            ConfigureDefaultSubscriptionProcessor(containerBuilder);

            containerBuilder
                .RegisterStorageAccount<PromotionConfiguration>(configuration => configuration.PackageStorageConnectionString)
                .Keyed<ICloudBlobClient>(PackageStorageKey);
            containerBuilder
                .RegisterStorageAccount<PromotionConfiguration>(configuration => configuration.StagingStorageConnectionString)
                .Keyed<ICloudBlobClient>(StagingStorageKey);
            containerBuilder
                .RegisterStorageAccount<PromotionConfiguration>(configuration => configuration.FlatContainerStorageConnectionString)
                .Keyed<ICloudBlobClient>(FlatContainerStorageKey);

            RegisterFileStorageService(containerBuilder, PackageStorageKey);
            RegisterFileStorageService(containerBuilder, StagingStorageKey);
            RegisterFileStorageService(containerBuilder, FlatContainerStorageKey);

            containerBuilder
                .RegisterType<StagingBlobService>()
                .WithParameter(KeyedParameter<ICoreFileStorageService>(StagingStorageKey))
                .As<IStagingBlobService>();
            containerBuilder
                .RegisterType<PromotionContentFileMetadataService>()
                .As<IContentFileMetadataService>();
            containerBuilder
                .RegisterType<CoreLicenseFileService>()
                .WithParameter(KeyedParameter<ICoreFileStorageService>(FlatContainerStorageKey))
                .As<ICoreLicenseFileService>();
            containerBuilder
                .RegisterType<CoreReadmeFileService>()
                .WithParameter(KeyedParameter<ICoreFileStorageService>(FlatContainerStorageKey))
                .As<ICoreReadmeFileService>();
            containerBuilder
                .RegisterType<StagedPackagePromotionMessageHandler>()
                .WithParameter(KeyedParameter<ICoreFileStorageService>(PackageStorageKey))
                .As<IMessageHandler<StagedPackagePromotionMessage>>();
        }

        private static void RegisterFileStorageService(ContainerBuilder containerBuilder, string storageKey)
        {
            containerBuilder
                .RegisterType<CloudBlobCoreFileStorageService>()
                .WithParameter(KeyedParameter<ICloudBlobClient>(storageKey))
                .Keyed<ICoreFileStorageService>(storageKey);
        }

        private static ResolvedParameter KeyedParameter<T>(string key)
        {
            return new ResolvedParameter(
                (parameter, context) => parameter.ParameterType == typeof(T),
                (parameter, context) => context.ResolveKeyed<T>(key));
        }
    }
}

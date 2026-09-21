// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;
using NuGet.Services.ServiceBus;
using NuGet.Services.Staging;
using NuGetGallery;

namespace NuGet.Services.Staging.Promotion
{
    /// <summary>
    /// Hosts staged package promotion message processing.
    /// </summary>
    public class Job : SubscriptionProcessorJob<StagingPromotionMessage>
    {
        private const string PromotionConfigurationSectionName = "Promotion";
        private const string PackageStorageKey = "PackageStorage";
        private const string StagingStorageKey = "StagingStorage";
        private const string FlatContainerStorageKey = "FlatContainerStorage";
        private const string PromotionTopicKey = "PromotionTopic";

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
            services.AddTransient<IBrokeredMessageSerializer<StagingPromotionMessage>, StagingPromotionMessageSerializer>();
            services.AddTransient<ISubscriptionProcessorTelemetryService, SubscriptionProcessorNoTelemetryService>();
            services.AddTransient<ICloudBlobContainerInformationProvider, GalleryCloudBlobContainerInformationProvider>();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            ConfigureDefaultSubscriptionProcessor(containerBuilder);

            containerBuilder
                .Register(context =>
                {
                    var configuration = context.Resolve<IOptionsSnapshot<ServiceBusConfiguration>>().Value;
                    return new TopicClientWrapper(configuration.ConnectionString, configuration.TopicPath);
                })
                .Keyed<ITopicClient>(PromotionTopicKey)
                .SingleInstance()
                .OnRelease(client => _ = client.CloseAsync());
            containerBuilder
                .RegisterType<StagingPromotionMessageEnqueuer>()
                .WithKeyedParameter(typeof(ITopicClient), PromotionTopicKey)
                .As<IStagingPromotionMessageEnqueuer>();

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
                .WithKeyedParameter(typeof(ICoreFileStorageService), StagingStorageKey)
                .As<IStagingBlobService>();
            containerBuilder
                .RegisterType<PromotionContentFileMetadataService>()
                .As<IContentFileMetadataService>();
            containerBuilder
                .RegisterType<CoreLicenseFileService>()
                .WithKeyedParameter(typeof(ICoreFileStorageService), FlatContainerStorageKey)
                .As<ICoreLicenseFileService>();
            containerBuilder
                .RegisterType<CoreReadmeFileService>()
                .WithKeyedParameter(typeof(ICoreFileStorageService), FlatContainerStorageKey)
                .As<ICoreReadmeFileService>();
            containerBuilder
                .RegisterType<StagingGroupLockService>()
                .As<IStagingGroupLockService>();
            containerBuilder
                .RegisterType<StagingGroupPromotionService>()
                .As<IStagingGroupPromotionService>();
            containerBuilder
                .RegisterType<StagedPackagePromotionMessageHandler>()
                .WithKeyedParameter(typeof(ICoreFileStorageService), PackageStorageKey)
                .As<IStagingPromotionMessageHandler<StagedPackage>>();
            containerBuilder
                .RegisterType<StagingGroupPromotionMessageHandler>()
                .As<IStagingPromotionMessageHandler<StagingGroup>>();
            containerBuilder
                .RegisterType<StagingPromotionMessageHandler>()
                .As<IMessageHandler<StagingPromotionMessage>>();
        }

        private static void RegisterFileStorageService(ContainerBuilder containerBuilder, string storageKey)
        {
            containerBuilder
                .RegisterType<CloudBlobCoreFileStorageService>()
                .WithKeyedParameter(typeof(ICloudBlobClient), storageKey)
                .Keyed<ICoreFileStorageService>(storageKey);
        }
    }
}

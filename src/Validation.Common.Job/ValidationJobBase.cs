// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.Common;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Configuration;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;
using NuGetGallery;

namespace NuGet.Jobs.Validation
{
    public abstract class ValidationJobBase : JsonConfigurationJob
    {
        private const string PackageDownloadTimeoutName = "PackageDownloadTimeout";
        private const string PackageValidationServiceBusSectionName = "PackageValidationServiceBus";
        private const string PackageValidationServiceBusBindingKey = "PackageValidationServiceBusBindingKey";

        /// <summary>
        /// The maximum number of concurrent connections that can be established to a single server.
        /// </summary>
        private const int MaximumConnectionsPerServer = 64;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            ServicePointManager.DefaultConnectionLimit = MaximumConnectionsPerServer;
        }

        protected override void ConfigureDefaultJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            base.ConfigureDefaultJobServices(services, configurationRoot);

            ConfigureFeatureFlagServices(services, configurationRoot);
            ConfigureDatabaseServices(services);

            services.AddTransient<ICommonTelemetryService, CommonTelemetryService>();
            services.AddTransient<IFileDownloader, FileDownloader>();
            services.AddTransient<IServiceBusMessageSerializer, ServiceBusMessageSerializer>();

            services.AddTransient<ICloudBlobClient>(c =>
            {
                var configurationAccessor = c.GetRequiredService<IOptionsSnapshot<ValidationStorageConfiguration>>();
                return new CloudBlobClientWrapper(
                    configurationAccessor.Value.ConnectionString,
                    readAccessGeoRedundant: false);
            });
            services.AddTransient<ICoreFileStorageService, CloudBlobCoreFileStorageService>();
            services.AddTransient<ISharedAccessSignatureService, SharedAccessSignatureService>();

            services.AddTransient<ISubscriptionClient>(p =>
            {
                var config = p.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;

                return new SubscriptionClientWrapper(
                    config.ConnectionString,
                    config.TopicPath,
                    config.SubscriptionName,
                    p.GetRequiredService<ILogger<SubscriptionClientWrapper>>());
            });

            services.Configure<PackageValidationServiceBusConfiguration>(configurationRoot.GetSection(PackageValidationServiceBusSectionName));

            services.AddSingleton(p =>
            {
                var assembly = Assembly.GetEntryAssembly();
                var assemblyName = assembly.GetName().Name;
                var assemblyVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

                var client = new HttpClient(new HttpClientHandler
                {
                    AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate),
                });

                client.Timeout = configurationRoot.GetValue<TimeSpan>(PackageDownloadTimeoutName);
                client.DefaultRequestHeaders.Add("User-Agent", $"{assemblyName}/{assemblyVersion}");

                return client;
            });
        }

        protected override void ConfigureDefaultAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            base.ConfigureDefaultAutofacServices(containerBuilder, configurationRoot);

            ConfigureFeatureFlagAutofacServices(containerBuilder);

            containerBuilder
                .Register(c =>
                {
                    var serviceBusConfiguration = c.Resolve<IOptionsSnapshot<PackageValidationServiceBusConfiguration>>();
                    var topicClient = new TopicClientWrapper(serviceBusConfiguration.Value.ConnectionString, serviceBusConfiguration.Value.TopicPath);
                    return topicClient;
                })
                .Keyed<TopicClientWrapper>(PackageValidationServiceBusBindingKey);

            containerBuilder
                .RegisterType<PackageValidationEnqueuer>()
                .WithParameter(new ResolvedParameter(
                    (pi, ctx) => pi.ParameterType == typeof(ITopicClient),
                    (pi, ctx) => ctx.ResolveKeyed<TopicClientWrapper>(PackageValidationServiceBusBindingKey)))
                .As<IPackageValidationEnqueuer>();
        }

        public static new void ConfigureFeatureFlagServices(IServiceCollection services, IConfigurationRoot configurationRoot = null)
        {
            JsonConfigurationJob.ConfigureFeatureFlagServices(services, configurationRoot);
            services.AddTransient<IFeatureFlagService, FeatureFlagService>();
        }

        private void ConfigureDatabaseServices(IServiceCollection services)
        {
            services.AddScoped<IValidationEntitiesContext>(p =>
            {
                var connectionFactory = p.GetRequiredService<ISqlConnectionFactory<ValidationDbConfiguration>>();
                var connection = connectionFactory.CreateAsync().GetAwaiter().GetResult();

                return new ValidationEntitiesContext(connection);
            });

            services.AddScoped<IEntitiesContext>(p =>
            {
                var connectionFactory = p.GetRequiredService<ISqlConnectionFactory<GalleryDbConfiguration>>();
                DbConnection  connection = connectionFactory.CreateAsync().GetAwaiter().GetResult();

                return new EntitiesContext(connection, readOnly: true);
            });
        }
    }
}

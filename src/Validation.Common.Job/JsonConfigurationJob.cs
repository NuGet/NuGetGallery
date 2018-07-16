// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.Common;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Configuration;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGet.Services.Logging;
using NuGet.Services.ServiceBus;
using NuGet.Services.Sql;
using NuGet.Services.Validation;
using NuGetGallery;
using NuGetGallery.Diagnostics;

namespace NuGet.Jobs.Validation
{
    public abstract class JsonConfigurationJob : JobBase
    {
        private const string GalleryDbConfigurationSectionName = "GalleryDb";
        private const string ValidationDbConfigurationSectionName = "ValidationDb";
        private const string ServiceBusConfigurationSectionName = "ServiceBus";
        private const string ValidationStorageConfigurationSectionName = "ValidationStorage";
        private const string PackageDownloadTimeoutName = "PackageDownloadTimeout";

        /// <summary>
        /// The maximum number of concurrent connections that can be established to a single server.
        /// </summary>
        private const int MaximumConnectionsPerServer = 64;

        /// <summary>
        /// The argument this job uses to determine the configuration file's path.
        /// </summary>
        private const string ConfigurationArgument = "Configuration";

        /// <summary>
        /// The configured service provider, used to instantiate the services this job depends on.
        /// </summary>
        protected IServiceProvider _serviceProvider;

        /// <summary>
        /// The maximum time that a KeyVault secret will be cached for.
        /// </summary>
        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromDays(1);

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            var configurationRoot = GetConfigurationRoot(configurationFilename, out var secretInjector);

            _serviceProvider = GetServiceProvider(configurationRoot, secretInjector);

            ServicePointManager.DefaultConnectionLimit = MaximumConnectionsPerServer;
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename, out ISecretInjector secretInjector)
        {
            Logger.LogInformation(
                "Using the {ConfigurationFilename} configuration file",
                Path.Combine(Environment.CurrentDirectory, configurationFilename));

            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: true);

            var uninjectedConfiguration = builder.Build();

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var cachingSecretReaderFactory = new CachingSecretReaderFactory(secretReaderFactory, KeyVaultSecretCachingTimeout);
            secretInjector = cachingSecretReaderFactory.CreateSecretInjector(cachingSecretReaderFactory.CreateSecretReader());

            builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddInjectedJsonFile(configurationFilename, secretInjector);

            return builder.Build();
        }

        private IServiceProvider GetServiceProvider(IConfigurationRoot configurationRoot, ISecretInjector secretInjector)
        {
            // Configure as much as possible with Microsoft.Extensions.DependencyInjection.
            var services = new ServiceCollection();
            services.AddSingleton(secretInjector);

            ConfigureLibraries(services);
            ConfigureDefaultJobServices(services, configurationRoot);
            ConfigureJobServices(services, configurationRoot);

            // Configure the rest with Autofac.
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);

            ConfigureAutofacServices(containerBuilder);

            return new AutofacServiceProvider(containerBuilder.Build());
        }

        protected virtual DbConnection CreateDbConnection<T>(IServiceProvider serviceProvider) where T : IDbConfiguration
        {
            var connectionString = serviceProvider.GetRequiredService<IOptionsSnapshot<T>>().Value.ConnectionString;
            var connectionFactory = new AzureSqlConnectionFactory(connectionString,
                serviceProvider.GetRequiredService<ISecretInjector>());

            return Task.Run(() => connectionFactory.CreateAsync()).Result;
        }

        private void ConfigureDefaultJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<GalleryDbConfiguration>(configurationRoot.GetSection(GalleryDbConfigurationSectionName));
            services.Configure<ValidationDbConfiguration>(configurationRoot.GetSection(ValidationDbConfigurationSectionName));
            services.Configure<ServiceBusConfiguration>(configurationRoot.GetSection(ServiceBusConfigurationSectionName));
            services.Configure<ValidationStorageConfiguration>(configurationRoot.GetSection(ValidationStorageConfigurationSectionName));

            services.AddSingleton(new TelemetryClient());
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();
            services.AddTransient<ICommonTelemetryService, CommonTelemetryService>();
            services.AddTransient<IDiagnosticsService, LoggerDiagnosticsService>();
            services.AddTransient<IFileDownloader, PackageDownloader>();

            services.AddTransient<ICloudBlobClient>(c =>
            {
                var configurationAccessor = c.GetRequiredService<IOptionsSnapshot<ValidationStorageConfiguration>>();
                return new CloudBlobClientWrapper(
                    configurationAccessor.Value.ConnectionString,
                    readAccessGeoRedundant: false);
            });
            services.AddTransient<ICoreFileStorageService, CloudBlobCoreFileStorageService>();

            services.AddScoped<IValidationEntitiesContext>(p =>
            {
                return new ValidationEntitiesContext(CreateDbConnection<ValidationDbConfiguration>(p));
            });

            services.AddScoped<IEntitiesContext>(p =>
            {
                return new EntitiesContext(CreateDbConnection<GalleryDbConfiguration>(p), readOnly: true);
            });

            services.AddTransient<ISubscriptionClient>(p =>
            {
                var config = p.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;

                return new SubscriptionClientWrapper(config.ConnectionString, config.TopicPath, config.SubscriptionName);
            });

            services.AddSingleton(p =>
            {
                var assembly = Assembly.GetEntryAssembly();
                var assemblyName = assembly.GetName().Name;
                var assemblyVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

                var client = new HttpClient(new WebRequestHandler
                {
                    AllowPipelining = true,
                    AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate),
                });

                client.Timeout = configurationRoot.GetValue<TimeSpan>(PackageDownloadTimeoutName);
                client.DefaultRequestHeaders.Add("User-Agent", $"{assemblyName}/{assemblyVersion}");

                return client;
            });
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            // Use the custom NonCachingOptionsSnapshot so that KeyVault secret injection works properly.
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddSingleton(LoggerFactory);
            services.AddLogging();
        }

        /// <summary>
        /// Method to be implemented in derived classes to provide Autofac-specific configuration for
        /// that specific job (like setting up keyed resolution).
        /// </summary>
        protected abstract void ConfigureAutofacServices(ContainerBuilder containerBuilder);

        /// <summary>
        /// Method to be implemented in derived classes to provide DI container configuration
        /// specific for the job.
        /// </summary>
        protected abstract void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot);
    }
}

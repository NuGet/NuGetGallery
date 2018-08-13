// Copyright (c) .NET Foundation. All rights reserved.
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics.Tracing;
using System.IO;
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

namespace NuGet.Jobs
{
    public abstract class JsonConfigurationJob : JobBase
    {
        private const string InitializationConfigurationSectionName = "Initialization";
        private const string GalleryDbConfigurationSectionName = "GalleryDb";
        private const string StatisticsDbConfigurationSectionName = "StatisticsDb";
        private const string ValidationDbConfigurationSectionName = "ValidationDb";
        private const string ServiceBusConfigurationSectionName = "ServiceBus";
        private const string ValidationStorageConfigurationSectionName = "ValidationStorage";

        public JsonConfigurationJob()
            : this(null)
        {
        }

        public JsonConfigurationJob(EventSource jobEventSource)
            : base(jobEventSource)
        {
        }

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

            RegisterDatabases(_serviceProvider);
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

        protected virtual void ConfigureDefaultJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<GalleryDbConfiguration>(configurationRoot.GetSection(GalleryDbConfigurationSectionName));
            services.Configure<StatisticsDbConfiguration>(configurationRoot.GetSection(StatisticsDbConfigurationSectionName));
            services.Configure<ValidationDbConfiguration>(configurationRoot.GetSection(ValidationDbConfigurationSectionName));
            services.Configure<ServiceBusConfiguration>(configurationRoot.GetSection(ServiceBusConfigurationSectionName));
            services.Configure<ValidationStorageConfiguration>(configurationRoot.GetSection(ValidationStorageConfigurationSectionName));

            services.AddSingleton(new TelemetryClient());
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();

            services.AddScoped<ISqlConnectionFactory<GalleryDbConfiguration>>(p =>
            {
                return new DelegateSqlConnectionFactory<GalleryDbConfiguration>(
                    CreateSqlConnectionAsync<GalleryDbConfiguration>,
                    p.GetRequiredService<ILogger<DelegateSqlConnectionFactory<GalleryDbConfiguration>>>());
            });

            services.AddScoped<ISqlConnectionFactory<StatisticsDbConfiguration>>(p =>
            {
                return new DelegateSqlConnectionFactory<StatisticsDbConfiguration>(
                    CreateSqlConnectionAsync<StatisticsDbConfiguration>,
                    p.GetRequiredService<ILogger<DelegateSqlConnectionFactory<StatisticsDbConfiguration>>>());
            });

            services.AddScoped<ISqlConnectionFactory<ValidationDbConfiguration>>(p =>
            {
                return new DelegateSqlConnectionFactory<ValidationDbConfiguration>(
                    CreateSqlConnectionAsync<ValidationDbConfiguration>,
                    p.GetRequiredService<ILogger<DelegateSqlConnectionFactory<ValidationDbConfiguration>>>());
            });
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            // Use the custom NonCachingOptionsSnapshot so that KeyVault secret injection works properly.
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddSingleton(LoggerFactory);
            services.AddLogging();
        }

        protected virtual void RegisterDatabases(IServiceProvider serviceProvider)
        {
            var galleryDb = serviceProvider.GetRequiredService<IOptionsSnapshot<GalleryDbConfiguration>>();
            if (!string.IsNullOrEmpty(galleryDb.Value?.ConnectionString))
            {
                RegisterDatabase<GalleryDbConfiguration>(serviceProvider);
            }

            var statisticsDb = serviceProvider.GetRequiredService<IOptionsSnapshot<StatisticsDbConfiguration>>();
            if (!string.IsNullOrEmpty(statisticsDb.Value?.ConnectionString))
            {
                RegisterDatabase<StatisticsDbConfiguration>(serviceProvider);
            }

            var validationDb = serviceProvider.GetRequiredService<IOptionsSnapshot<ValidationDbConfiguration>>();
            if (!string.IsNullOrEmpty(validationDb.Value?.ConnectionString))
            {
                RegisterDatabase<ValidationDbConfiguration>(serviceProvider);
            }
        }

        protected virtual void ConfigureInitializationSection<TConfiguration>(
            IServiceCollection services,
            IConfigurationRoot configurationRoot)
            where TConfiguration : class
        {
            services.Configure<TConfiguration>(configurationRoot.GetSection(InitializationConfigurationSectionName));
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

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
using NuGet.Services.FeatureFlags;
using NuGet.Services.KeyVault;
using NuGet.Services.Logging;
using NuGetGallery;
using NuGetGallery.Diagnostics;
using NuGetGallery.Features;

namespace NuGet.Jobs
{
    public abstract class JsonConfigurationJob : JobBase
    {
        private const string InitializationConfigurationSectionName = "Initialization";
        private const string GalleryDbConfigurationSectionName = "GalleryDb";
        private const string StatisticsDbConfigurationSectionName = "StatisticsDb";
        private const string SupportRequestDbConfigurationSectionName = "SupportRequestDb";
        private const string ValidationDbConfigurationSectionName = "ValidationDb";
        private const string ServiceBusConfigurationSectionName = "ServiceBus";
        private const string ValidationStorageConfigurationSectionName = "ValidationStorage";
        private const string FeatureFlagConfigurationSectionName = "FeatureFlags";

        private const string FeatureFlagBindingKey = nameof(FeatureFlagBindingKey);

        private bool testDatabaseConnections = true;

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
        private static readonly TimeSpan KeyVaultSecretCachingTimeout = TimeSpan.FromHours(6);

        /// <summary>
        /// Skip secret injection allowing the job to do some consistency checks without needing secret injection. This
        /// is useful if you want to run some code in the CI or in some other environment that doesn't have access to
        /// secrets or other secured resources.
        /// </summary>
        protected bool _validateOnly;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            var configurationRoot = GetConfigurationRoot(configurationFilename, out var secretInjector, out var secretReader);

            if (_serviceProvider != null && _serviceProvider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
            _serviceProvider = GetServiceProvider(configurationRoot, secretInjector, secretReader);

            if (!_validateOnly)
            {
                RegisterDatabases(_serviceProvider);
            }
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename, out ICachingSecretInjector secretInjector, out ICachingSecretReader secretReader)
        {
            Logger.LogInformation(
                "Using the {ConfigurationFilename} configuration file",
                Path.Combine(Environment.CurrentDirectory, configurationFilename));

            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile(configurationFilename, optional: false, reloadOnChange: false);

            var uninjectedConfiguration = builder.Build();

            if (_validateOnly)
            {
                secretInjector = null;
                secretReader = null;
                return uninjectedConfiguration;
            }

            var secretReaderFactory = new ConfigurationRootSecretReaderFactory(uninjectedConfiguration);
            var cachingSecretReaderFactory = new CachingSecretReaderFactory(secretReaderFactory, KeyVaultSecretCachingTimeout);
            secretReader = cachingSecretReaderFactory.CreateSecretReader() as ICachingSecretReader;
            secretInjector = cachingSecretReaderFactory.CreateSecretInjector(cachingSecretReaderFactory.CreateSecretReader()) as ICachingSecretInjector;

            builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddInjectedJsonFile(configurationFilename, secretInjector);

            return builder.Build();
        }

        private IServiceProvider GetServiceProvider(IConfigurationRoot configurationRoot, ICachingSecretInjector secretInjector, ICachingSecretReader secretReader)
        {
            // Configure as much as possible with Microsoft.Extensions.DependencyInjection.
            var services = new ServiceCollection();

            if (!_validateOnly)
            {
                services.AddSingleton<ISecretInjector>(secretInjector);
                services.AddSingleton(secretInjector);
                services.AddSingleton<ISecretReader>(secretReader);
                services.AddSingleton(secretReader);
            }

            services.AddSingleton<IConfiguration>(configurationRoot);

            ConfigureLibraries(services);
            ConfigureDefaultJobServices(services, configurationRoot);
            ConfigureJobServices(services, configurationRoot);

            // Configure the rest with Autofac.
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);
            containerBuilder.RegisterAssemblyModules(GetType().Assembly);

            // Classes below implement IDisposable, so we'll tell Autofac
            // not to dispose of it when container is disposed of. Otherwise, on second and
            // subsequent job runs we'll end up with them disposed.
            containerBuilder
                .RegisterInstance(ApplicationInsightsConfiguration.TelemetryConfiguration)
                .ExternallyOwned();
            containerBuilder
                .RegisterInstance(LoggerFactory)
                .ExternallyOwned();

            ConfigureDefaultAutofacServices(containerBuilder, configurationRoot);
            ConfigureAutofacServices(containerBuilder, configurationRoot);

            return new AutofacServiceProvider(containerBuilder.Build());
        }

        protected virtual void ConfigureDefaultJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<GalleryDbConfiguration>(configurationRoot.GetSection(GalleryDbConfigurationSectionName));
            services.Configure<StatisticsDbConfiguration>(configurationRoot.GetSection(StatisticsDbConfigurationSectionName));
            services.Configure<SupportRequestDbConfiguration>(configurationRoot.GetSection(SupportRequestDbConfigurationSectionName));
            services.Configure<ValidationDbConfiguration>(configurationRoot.GetSection(ValidationDbConfigurationSectionName));
            services.Configure<ServiceBusConfiguration>(configurationRoot.GetSection(ServiceBusConfigurationSectionName));
            services.Configure<ValidationStorageConfiguration>(configurationRoot.GetSection(ValidationStorageConfigurationSectionName));

            services.AddSingleton(new TelemetryClient(ApplicationInsightsConfiguration.TelemetryConfiguration));
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();
            services.AddTransient<IDiagnosticsService, LoggerDiagnosticsService>();
            services.AddTransient<ICloudBlobContainerInformationProvider, GalleryCloudBlobContainerInformationProvider>();

            AddScopedSqlConnectionFactory<GalleryDbConfiguration>(services);
            AddScopedSqlConnectionFactory<StatisticsDbConfiguration>(services);
            AddScopedSqlConnectionFactory<SupportRequestDbConfiguration>(services);
            AddScopedSqlConnectionFactory<ValidationDbConfiguration>(services);
        }

        protected virtual void ConfigureDefaultAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }

        private void AddScopedSqlConnectionFactory<TDbConfiguration>(IServiceCollection services)
            where TDbConfiguration : IDbConfiguration
        {
            services.AddScoped<ISqlConnectionFactory<TDbConfiguration>>(p =>
            {
                return new DelegateSqlConnectionFactory<TDbConfiguration>(
                    CreateSqlConnectionAsync<TDbConfiguration>,
                    p.GetRequiredService<ILogger<DelegateSqlConnectionFactory<TDbConfiguration>>>());
            });
        }

        public static void ConfigureFeatureFlagAutofacServices(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .Register(c =>
                {
                    var options = c.Resolve<IOptionsSnapshot<FeatureFlagConfiguration>>();
                    return new CloudBlobClientWrapper(
                        options.Value.ConnectionString,
                        requestTimeout: TimeSpan.FromMinutes(2));
                })
                .Keyed<ICloudBlobClient>(FeatureFlagBindingKey);

            containerBuilder
                .Register(c => new CloudBlobCoreFileStorageService(
                    c.ResolveKeyed<ICloudBlobClient>(FeatureFlagBindingKey),
                    c.Resolve<IDiagnosticsService>(),
                    c.Resolve<ICloudBlobContainerInformationProvider>()))
                .Keyed<ICoreFileStorageService>(FeatureFlagBindingKey);

            containerBuilder
                .Register(c => new FeatureFlagFileStorageService(
                    c.ResolveKeyed<ICoreFileStorageService>(FeatureFlagBindingKey)))
                .As<IFeatureFlagStorageService>();
        }

        public static void ConfigureFeatureFlagServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<FeatureFlagConfiguration>(configurationRoot.GetSection(FeatureFlagConfigurationSectionName));

            services
                .AddTransient(p =>
                {
                    var options = p.GetRequiredService<IOptionsSnapshot<FeatureFlagConfiguration>>();
                    return new FeatureFlagOptions
                    {
                        RefreshInterval = options.Value.RefreshInternal,
                    };
                });

            services.AddTransient<IFeatureFlagClient, FeatureFlagClient>();
            services.AddTransient<ICloudBlobContainerInformationProvider, GalleryCloudBlobContainerInformationProvider>();
            services.AddTransient<IFeatureFlagTelemetryService, FeatureFlagTelemetryService>();

            services.AddSingleton<IFeatureFlagCacheService, FeatureFlagCacheService>();
            services.AddSingleton<IFeatureFlagRefresher, FeatureFlagRefresher>();
        }

        private void ConfigureLibraries(IServiceCollection services)
        {
            // Use the custom NonCachingOptionsSnapshot so that KeyVault secret injection works properly.
            services.Add(ServiceDescriptor.Scoped(typeof(IOptionsSnapshot<>), typeof(NonCachingOptionsSnapshot<>)));
            services.AddLogging();
        }

        protected virtual void RegisterDatabases(IServiceProvider serviceProvider)
        {
            try
            {
                RegisterDatabaseIfConfigured<GalleryDbConfiguration>(serviceProvider, testDatabaseConnections);
                RegisterDatabaseIfConfigured<StatisticsDbConfiguration>(serviceProvider, testDatabaseConnections);
                RegisterDatabaseIfConfigured<SupportRequestDbConfiguration>(serviceProvider, testDatabaseConnections);
                RegisterDatabaseIfConfigured<ValidationDbConfiguration>(serviceProvider, testDatabaseConnections);
            }
            finally
            {
                testDatabaseConnections = false;
            }
        }

        private void RegisterDatabaseIfConfigured<TDbConfiguration>(IServiceProvider serviceProvider, bool testConnection)
            where TDbConfiguration : class, IDbConfiguration, new()
        {
            var dbConfiguration = serviceProvider.GetRequiredService<IOptionsSnapshot<TDbConfiguration>>();
            if (!string.IsNullOrEmpty(dbConfiguration.Value?.ConnectionString))
            {
                RegisterDatabase<TDbConfiguration>(serviceProvider, testConnection);
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
        protected abstract void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot);

        /// <summary>
        /// Method to be implemented in derived classes to provide DI container configuration
        /// specific for the job.
        /// </summary>
        protected abstract void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot);
    }
}

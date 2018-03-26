// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
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

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
            var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);

            _serviceProvider = GetServiceProvider(GetConfigurationRoot(configurationFilename));

            ServicePointManager.DefaultConnectionLimit = MaximumConnectionsPerServer;
        }

        private IConfigurationRoot GetConfigurationRoot(string configurationFilename)
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
            var secretInjector = cachingSecretReaderFactory.CreateSecretInjector(cachingSecretReaderFactory.CreateSecretReader());

            builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddInjectedJsonFile(configurationFilename, secretInjector);

            return builder.Build();
        }

        private IServiceProvider GetServiceProvider(IConfigurationRoot configurationRoot)
        {
            // Configure as much as possible with Microsoft.Extensions.DependencyInjection.
            var services = new ServiceCollection();

            ConfigureLibraries(services);
            ConfigureDefaultJobServices(services, configurationRoot);
            ConfigureJobServices(services, configurationRoot);

            // Configure the rest with Autofac.
            var containerBuilder = new ContainerBuilder();
            containerBuilder.Populate(services);

            ConfigureAutofacServices(containerBuilder);

            return new AutofacServiceProvider(containerBuilder.Build());
        }

        private void ConfigureDefaultJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<GalleryDbConfiguration>(configurationRoot.GetSection(GalleryDbConfigurationSectionName));
            services.Configure<ValidationDbConfiguration>(configurationRoot.GetSection(ValidationDbConfigurationSectionName));
            services.Configure<ServiceBusConfiguration>(configurationRoot.GetSection(ServiceBusConfigurationSectionName));

            services.AddSingleton(new TelemetryClient());
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();
            services.AddTransient<ICommonTelemetryService, CommonTelemetryService>();
            services.AddTransient<IDiagnosticsService, LoggerDiagnosticsService>();
            services.AddTransient<IPackageDownloader, PackageDownloader>();

            services.AddScoped<IValidationEntitiesContext>(p =>
            {
                var config = p.GetRequiredService<IOptionsSnapshot<ValidationDbConfiguration>>().Value;

                return new ValidationEntitiesContext(config.ConnectionString);
            });

            services.AddScoped<IEntitiesContext>(p =>
            {
                var config = p.GetRequiredService<IOptionsSnapshot<GalleryDbConfiguration>>().Value;

                return new EntitiesContext(config.ConnectionString, readOnly: true);
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

        protected abstract void ConfigureAutofacServices(ContainerBuilder containerBuilder);
        protected abstract void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot);
    }
}

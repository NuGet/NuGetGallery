// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Jobs.Validation;
using NuGet.Services.Logging;
using NuGet.Services.ServiceBus;
using NuGet.Services.Validation;
using NuGetGallery;

namespace NuGet.Services.Revalidate
{
    using GalleryContext = EntitiesContext;
    using IGalleryContext = IEntitiesContext;

    public class Job : ValidationJobBase
    {
        private const string RebuildPreinstalledSetArgumentName = "RebuildPreinstalledSet";
        private const string InitializeArgumentName = "Initialize";
        private const string VerifyInitializationArgumentName = "VerifyInitialization";

        private const string JobConfigurationSectionName = "RevalidateJob";

        private static readonly TimeSpan RetryLaterSleepDuration = TimeSpan.FromMinutes(5);

        private string _preinstalledSetPath;
        private bool _initialize;
        private bool _verifyInitialization;

        public override void Init(IServiceContainer serviceContainer, IDictionary<string, string> jobArgsDictionary)
        {
            base.Init(serviceContainer, jobArgsDictionary);

            _preinstalledSetPath = JobConfigurationManager.TryGetArgument(jobArgsDictionary, RebuildPreinstalledSetArgumentName);
            _initialize = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, InitializeArgumentName);
            _verifyInitialization = JobConfigurationManager.TryGetBoolArgument(jobArgsDictionary, VerifyInitializationArgumentName);
        }

        public override async Task Run()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                if (!string.IsNullOrEmpty(_preinstalledSetPath))
                {
                    Logger.LogInformation("Rebuilding the preinstalled packages set...");

                    var config = scope.ServiceProvider.GetRequiredService<InitializationConfiguration>();
                    var preinstalledPackagesNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var path in config.PreinstalledPaths)
                    {
                        var expandedPath = Environment.ExpandEnvironmentVariables(path);
                        var packagesInPath = Directory.GetDirectories(expandedPath)
                            .Select(d => d.Replace(expandedPath, "").Trim('\\').ToLowerInvariant())
                            .Where(d => !d.StartsWith("."));

                        preinstalledPackagesNames.UnionWith(packagesInPath);
                    }
                        
                    File.WriteAllText(_preinstalledSetPath, JsonConvert.SerializeObject(preinstalledPackagesNames));

                    Logger.LogInformation("Rebuilt the preinstalled package set. Found {PreinstalledPackages} package ids", preinstalledPackagesNames.Count);
                }
                else if (_initialize || _verifyInitialization)
                {
                    var initializer = scope.ServiceProvider.GetRequiredService<InitializationManager>();

                    if (_initialize)
                    {
                        Logger.LogInformation("Initializing Revalidate job...");

                        await initializer.InitializeAsync();

                        Logger.LogInformation("Revalidate job initialized");
                    }

                    if (_verifyInitialization)
                    {
                        Logger.LogInformation("Verifying initialization...");

                        await initializer.VerifyInitializationAsync();

                        Logger.LogInformation("Initialization verified");
                    }
                }
                else
                {
                    Logger.LogInformation("Running the revalidation service...");

                    await scope.ServiceProvider
                        .GetRequiredService<IRevalidationService>()
                        .RunAsync();

                    Logger.LogInformation("Revalidation service finished running");
                }
            }
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<RevalidationConfiguration>(configurationRoot.GetSection(JobConfigurationSectionName));

            services.AddSingleton(provider => provider.GetRequiredService<IOptionsSnapshot<RevalidationConfiguration>>().Value);
            services.AddSingleton(provider => provider.GetRequiredService<IOptionsSnapshot<RevalidationConfiguration>>().Value.Initialization);
            services.AddSingleton(provider => provider.GetRequiredService<IOptionsSnapshot<RevalidationConfiguration>>().Value.Health);
            services.AddSingleton(provider => provider.GetRequiredService<IOptionsSnapshot<RevalidationConfiguration>>().Value.AppInsights);
            services.AddSingleton(provider => provider.GetRequiredService<IOptionsSnapshot<RevalidationConfiguration>>().Value.Queue);

            services.AddScoped<IGalleryContext>(provider =>
            {
                var connectionFactory = provider.GetRequiredService<ISqlConnectionFactory<GalleryDbConfiguration>>();
                var connection = connectionFactory.CreateAsync().GetAwaiter().GetResult();

                return new GalleryContext(connection, readOnly: false);
            });

            // Core
            services.AddTransient<ITelemetryService, TelemetryService>();
            services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();

            services.AddTransient<IPackageRevalidationStateService, PackageRevalidationStateService>();
            services.AddTransient<IPackageRevalidationInserter, PackageRevalidationInserter>();
            services.AddTransient<IRevalidationJobStateService, RevalidationJobStateService>();
            services.AddTransient<IRevalidationStateService, RevalidationStateService>();

            // Initialization
            services.AddTransient<IPackageFinder, PackageFinder>();
            services.AddTransient<InitializationManager>();

            // Revalidation
            services.AddTransient<IGalleryService, GalleryService>();
            services.AddTransient<IHealthService, HealthService>();
            services.AddTransient<IRevalidationQueue, RevalidationQueue>();
            services.AddTransient<IRevalidationService, RevalidationService>();
            services.AddTransient<IRevalidationStarter, RevalidationStarter>();
            services.AddTransient<IRevalidationThrottler, RevalidationThrottler>();
            services.AddTransient<ISingletonService, SingletonService>();

            services.AddTransient<IPackageValidationEnqueuer, PackageValidationEnqueuer>();
            services.AddTransient<IServiceBusMessageSerializer, ServiceBusMessageSerializer>();
            services.AddTransient<ITopicClient>(provider =>
            {
                var config = provider.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;

                return new TopicClientWrapper(config.ConnectionString, config.TopicPath);
            });
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
        }
    }
}
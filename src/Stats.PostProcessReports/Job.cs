// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using NuGet.Services.Storage;

namespace Stats.PostProcessReports
{
    public class Job : JsonConfigurationJob
    {
        public override async Task Run()
        {
            var detailedReportPostProcessor = _serviceProvider.GetRequiredService<IDetailedReportPostProcessor>();
            await detailedReportPostProcessor.CopyReportsAsync();
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<PostProcessReportsConfiguration>(configurationRoot.GetSection("Configuration"));

            services.AddTransient<ITelemetryService, TelemetryService>();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            const string sourceKey = "SourceStorageKey";
            const string workKey = "WorkStorageKey";
            const string destinationKey = "DestinationStorageKey";

            containerBuilder
                .Register(c =>
                {
                    var cfg = c.Resolve<IOptionsSnapshot<PostProcessReportsConfiguration>>().Value;
                    return CloudStorageAccount.Parse(cfg.StorageAccount);
                })
                .AsSelf();

            containerBuilder
                .Register(c =>
                {
                    var cfg = c.Resolve<IOptionsSnapshot<PostProcessReportsConfiguration>>().Value;
                    var factory = new AzureStorageFactory(
                        c.Resolve<CloudStorageAccount>(),
                        cfg.SourceContainerName,
                        c.Resolve<ILogger<AzureStorage>>(),
                        cfg.SourcePath + cfg.DetailedReportDirectoryName,
                        useServerSideCopy: true,
                        initializeContainer: false);
                    var storage = factory.Create();
                    storage.Verbose = false;
                    return storage;
                })
                .Keyed<IStorage>(sourceKey);

            containerBuilder
                .Register(c =>
                {
                    var cfg = c.Resolve<IOptionsSnapshot<PostProcessReportsConfiguration>>().Value;
                    var factory = new AzureStorageFactory(
                        c.Resolve<CloudStorageAccount>(),
                        cfg.WorkContainerName,
                        c.Resolve<ILogger<AzureStorage>>(),
                        cfg.WorkPath,
                        useServerSideCopy: true,
                        initializeContainer: false);
                    var storage = factory.Create();
                    storage.Verbose = false;
                    return storage;
                })
                .Keyed<IStorage>(workKey);

            containerBuilder
                .Register(c =>
                {
                    var cfg = c.Resolve<IOptionsSnapshot<PostProcessReportsConfiguration>>().Value;
                    var factory = new AzureStorageFactory(
                        c.Resolve<CloudStorageAccount>(),
                        cfg.DestinationContainerName,
                        c.Resolve<ILogger<AzureStorage>>(),
                        cfg.DestinationPath,
                        useServerSideCopy: true,
                        initializeContainer: false);
                    var storage = factory.Create();
                    storage.Verbose = false;
                    return storage;
                })
                .Keyed<IStorage>(destinationKey);

            containerBuilder
                .RegisterType<DetailedReportPostProcessor>()
                .WithParameter(new ResolvedParameter(
                    (pi, _) => pi.ParameterType == typeof(IStorage) && pi.Name == "sourceStorage",
                    (_, ctx) => ctx.ResolveKeyed<IStorage>(sourceKey)))
                .WithParameter(new ResolvedParameter(
                    (pi, _) => pi.ParameterType == typeof(IStorage) && pi.Name == "workStorage",
                    (_, ctx) => ctx.ResolveKeyed<IStorage>(workKey)))
                .WithParameter(new ResolvedParameter(
                    (pi, _) => pi.ParameterType == typeof(IStorage) && pi.Name == "destinationStorage",
                    (_, ctx) => ctx.ResolveKeyed<IStorage>(destinationKey)))
                .As<IDetailedReportPostProcessor>();
        }

    }
}

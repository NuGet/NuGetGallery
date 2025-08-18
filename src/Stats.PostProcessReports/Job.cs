// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            services.ConfigureStorageMsi(configurationRoot);
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
                    PostProcessReportsConfiguration cfg = c.Resolve<IOptionsSnapshot<PostProcessReportsConfiguration>>().Value;
                    StorageMsiConfiguration storageMsiConfiguration = _serviceProvider.GetRequiredService<IOptionsSnapshot<StorageMsiConfiguration>>().Value;
                    try
                    {
                        if (storageMsiConfiguration.UseManagedIdentity)
                        {
                            string connectionString = cfg.StorageAccount.Replace("BlobEndPoint=", "");
                            Uri blobEndpointUri = new Uri(connectionString);

                            if (string.IsNullOrWhiteSpace(storageMsiConfiguration.ManagedIdentityClientId))
                            {
                                // 1. Using MSI with DefaultAzureCredential (local debugging)
                                return new BlobServiceClientFactory(blobEndpointUri, new DefaultAzureCredential());
                            }
                            else
                            {
                                // 2. Using MSI with ClientId
                                return new BlobServiceClientFactory(blobEndpointUri, new ManagedIdentityCredential(storageMsiConfiguration.ManagedIdentityClientId));
                            }
                        }
                        else
                        {
                            // 3. Using SAS token
                            return new BlobServiceClientFactory(AzureStorageFactory.PrepareConnectionString(cfg.StorageAccount));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException("Job parameter for Azure CDN Blob Service Client is invalid.", ex);
                    }
                })
                .AsSelf();

            containerBuilder
                .Register(c =>
                {
                    var cfg = c.Resolve<IOptionsSnapshot<PostProcessReportsConfiguration>>().Value;
                    var factory = new AzureStorageFactory(
                        c.Resolve<BlobServiceClientFactory>(),
                        cfg.SourceContainerName,
                        enablePublicAccess: false,
                        c.Resolve<ILogger<AzureStorage>>(),
                        cfg.SourcePath + cfg.DetailedReportDirectoryName,
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
                        c.Resolve<BlobServiceClientFactory>(),
                        cfg.WorkContainerName,
                        enablePublicAccess: false,
                        c.Resolve<ILogger<AzureStorage>>(),
                        cfg.WorkPath,
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
                        c.Resolve<BlobServiceClientFactory>(),
                        cfg.DestinationContainerName,
                        enablePublicAccess: true,
                        c.Resolve<ILogger<AzureStorage>>(),
                        cfg.DestinationPath,
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

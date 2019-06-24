// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using NuGet.Packaging.Core;
using NuGet.Services.Configuration;
using NuGet.Services.Metadata.Catalog;

namespace Ng.Jobs
{
    public class Catalog2PackageFixupJob : NgJob
    {
        private const int MaximumPackageProcessingAttempts = 5;
        private static readonly TimeSpan MaximumPackageProcessingTime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan DonePollingInterval = TimeSpan.FromMinutes(1);

        private IServiceProvider _serviceProvider;

        public Catalog2PackageFixupJob(ITelemetryService telemetryService, ILoggerFactory loggerFactory)
            : base(telemetryService, loggerFactory)
        {
            ThreadPool.SetMinThreads(MaxDegreeOfParallelism, 4);
        }

        public override string GetUsage()
        {
            return "Usage: ng catalog2packagefixup"
                   + $"-{Arguments.Source} <catalog>"
                   + $"-{Arguments.Verify} true/false"
                   + $"-{Arguments.StorageAccountName} <azure-account>"
                   + $"-{Arguments.StorageKeyValue} <azure-key> "
                   + $"-{Arguments.StorageContainer} <azure-container>";
        }

        protected override void Init(IDictionary<string, string> arguments, CancellationToken cancellationToken)
        {
            var source = arguments.GetOrThrow<string>(Arguments.Source);
            var storageAccount = arguments.GetOrThrow<string>(Arguments.StorageAccountName);
            var storageKey = arguments.GetOrThrow<string>(Arguments.StorageKeyValue);
            var storageContainer = arguments.GetOrThrow<string>(Arguments.StorageContainer);
            var verify = arguments.GetOrDefault(Arguments.Verify, false);
            var verbose = arguments.GetOrDefault(Arguments.Verbose, false);

            var services = new ServiceCollection();

            services.AddSingleton(TelemetryService);
            services.AddSingleton(LoggerFactory);
            services.AddLogging();

            // Prepare the HTTP Client
            services.AddSingleton(p =>
            {
                var httpClient = new HttpClient(new WebRequestHandler
                {
                    AllowPipelining = true
                });
                
                httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgentUtility.GetUserAgent());
                httpClient.Timeout = TimeSpan.FromMinutes(10);

                return httpClient;
            });

            // Prepare the catalog reader.
            services.AddSingleton(p =>
            {
                var telemetryService = p.GetRequiredService<ITelemetryService>();
                var httpMessageHandlerFactory = CommandHelpers.GetHttpMessageHandlerFactory(telemetryService, verbose);

                return new CollectorHttpClient(httpMessageHandlerFactory());
            });

            services.AddTransient(p =>
            {
                var collectorHttpClient = p.GetRequiredService<CollectorHttpClient>();
                var telemetryService = p.GetRequiredService<ITelemetryService>();

                return new CatalogIndexReader(new Uri(source), collectorHttpClient, telemetryService);
            });

            // Prepare the Azure Blob Storage container.
            services.AddSingleton(p =>
            {
                var credentials = new StorageCredentials(storageAccount, storageKey);
                var account = new CloudStorageAccount(credentials, useHttps: true);

                return account
                    .CreateCloudBlobClient()
                    .GetContainerReference(storageContainer);
            });

            // Prepare the handler that will run on each catalog entry.
            if (verify)
            {
                Logger.LogInformation("Validating that all packages have the proper Content MD5 hash...");

                services.AddTransient<IPackagesContainerHandler, ValidatePackageHashHandler>();
            }
            else
            {
                Logger.LogInformation("Ensuring all packages have a Content MD5 hash...");

                services.AddTransient<IPackagesContainerHandler, FixPackageHashHandler>();
            }

            services.AddTransient<PackagesContainerCatalogProcessor>();

            _serviceProvider = services.BuildServiceProvider();
        }

        protected override async Task RunInternalAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Parsing catalog for all entries.");

            var catalogReader = _serviceProvider.GetRequiredService<CatalogIndexReader>();
            var entries = await catalogReader.GetEntries();

            var latestEntries = entries
                .GroupBy(c => new PackageIdentity(c.Id, c.Version))
                .Select(g => g.OrderByDescending(c => c.CommitTimeStamp).First())
                .Where(c => !c.IsDelete);

            var packageEntries = new ConcurrentBag<CatalogIndexEntry>(latestEntries);

            Logger.LogInformation("Processing packages.");

            var stopwatch = Stopwatch.StartNew();
            var processor = _serviceProvider.GetRequiredService<PackagesContainerCatalogProcessor>();
            var totalEntries = packageEntries.Count;

            var tasks = Enumerable
                .Range(0, MaxDegreeOfParallelism)
                .Select(async i =>
                {
                    while (packageEntries.TryTake(out var entry))
                    {
                        await processor.ProcessCatalogIndexEntryAsync(entry);
                    }
                })
                .ToList();

            tasks.Add(LogProgress(packageEntries));

            await Task.WhenAll(tasks);

            Logger.LogInformation(
                "Processed {ProcessedCount} packages in {ProcessDuration}",
                totalEntries,
                stopwatch.Elapsed);
        }

        private async Task LogProgress(ConcurrentBag<CatalogIndexEntry> packageEntries)
        {
            int remaining;
            do
            {
                await Task.Delay(TimeSpan.FromMinutes(1));

                remaining = packageEntries.Count;

                Logger.LogInformation("{Remaining} packages left to enqueue...", remaining);

            }
            while (remaining > 0);
        }
    }
}

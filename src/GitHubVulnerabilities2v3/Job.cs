// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using GitHubVulnerabilities2v3.Configuration;
using GitHubVulnerabilities2v3.Extensions;
using GitHubVulnerabilities2v3.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using NuGet.Services.Cursor;
using NuGet.Services.GitHub.Collector;
using NuGet.Services.GitHub.Configuration;
using NuGet.Services.GitHub.GraphQL;
using NuGet.Services.GitHub.Ingest;
using NuGet.Services.Storage;

namespace GitHubVulnerabilities2v3
{
    public class Job : JsonConfigurationJob, IDisposable
    {
        private readonly HttpClient _client = new HttpClient();
        private readonly ProductInfoHeaderValue _userAgent = new ProductInfoHeaderValue("NuGet.Jobs.GitHubVulnerabilities2v3", "1.0.0");

        public override async Task Run()
        {
            var collector = _serviceProvider.GetRequiredService<IAdvisoryCollector>();
            var cursor = _serviceProvider.GetRequiredService<ReadWriteCursor<DateTimeOffset>>();
            var config = _serviceProvider.GetRequiredService<GitHubVulnerabilities2v3Configuration>();
            await SetRunMode(cursor, config);
            await collector.ProcessAsync(CancellationToken.None, updateCursor: false);
        }

        private async Task SetRunMode(ReadWriteCursor<DateTimeOffset> cursor, GitHubVulnerabilities2v3Configuration jobConfig)
        {
            await cursor.Load(CancellationToken.None);
            if (DateTimeOffset.Compare(cursor.Value.AddDays(jobConfig.DaysBeforeBaseStale), DateTimeOffset.UtcNow) <= 0)
            {
                cursor.Value = DateTimeOffset.FromUnixTimeSeconds(0);
            }
            await cursor.Save(CancellationToken.None);
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<GitHubVulnerabilities2v3Configuration>(services, configurationRoot);
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            containerBuilder
                .RegisterAdapter<IOptionsSnapshot<GitHubVulnerabilities2v3Configuration>, GitHubVulnerabilities2v3Configuration>(c => c.Value);
            containerBuilder
                .RegisterAdapter<IOptionsSnapshot<GitHubVulnerabilities2v3Configuration>, GraphQLQueryConfiguration>(c => c.Value);

            ConfigureQueryServices(containerBuilder);
            ConfigureIngestionServices(containerBuilder);
            ConfigureCollectorServices(containerBuilder);
        }

        protected void ConfigureIngestionServices(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .RegisterType<GitHubVersionRangeParser>()
                .As<IGitHubVersionRangeParser>();

            containerBuilder
                .RegisterType<TelemetryService>()
                .As<ITelemetryService>();

            containerBuilder
                .RegisterType<BlobStorageVulnerabilityWriter>()
                .As<IVulnerabilityWriter>();

            containerBuilder
                .RegisterType<AdvisoryIngestor>()
                .As<IAdvisoryIngestor>();
        }

        protected void ConfigureQueryServices(ContainerBuilder containerBuilder)
        {
            _client.DefaultRequestHeaders.UserAgent.Add(_userAgent);
            containerBuilder
                .RegisterInstance(_client)
                .As<HttpClient>()
                .ExternallyOwned(); // We don't want autofac disposing this--see https://github.com/NuGet/NuGetGallery/issues/9194

            containerBuilder
                .RegisterType<QueryService>()
                .As<IQueryService>();

            containerBuilder
                .RegisterType<AdvisoryQueryBuilder>()
                .As<IAdvisoryQueryBuilder>();

            containerBuilder
                .RegisterType<AdvisoryQueryService>()
                .As<IAdvisoryQueryService>();
        }

        protected void ConfigureCollectorServices(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .Register(ctx =>
                {
                    var config = ctx.Resolve<GitHubVulnerabilities2v3Configuration>();
                    return CloudStorageAccount.Parse(config.StorageConnectionString);
                })
                .As<CloudStorageAccount>();

            containerBuilder
                .RegisterType<AzureStorageFactory>()
                .WithParameter(
                    (parameter, ctx) => parameter.Name == "containerName",
                    (parameter, ctx) => ctx.Resolve<GitHubVulnerabilities2v3Configuration>().V3VulnerabilityContainerName)
                .As<StorageFactory>()
                .As<IStorageFactory>();

            containerBuilder
                .Register(ctx => CreateCursor(ctx, config => config.AdvisoryCursorBlobName))
                .As<ReadWriteCursor<DateTimeOffset>>();

            containerBuilder
                .RegisterType<AdvisoryCollector>()
                .As<IAdvisoryCollector>();
        }

        private DurableCursor CreateCursor(IComponentContext ctx, Func<GitHubVulnerabilities2v3Configuration, string> getBlobName)
        {
            var config = ctx.Resolve<IOptionsSnapshot<GitHubVulnerabilities2v3Configuration>>().Value;
            var storageFactory = ctx.Resolve<IStorageFactory>();
            var storage = storageFactory.Create();
            return new DurableCursor(storage.ResolveUri(getBlobName(config)), storage, DateTimeOffset.MinValue);
        }
    }
}
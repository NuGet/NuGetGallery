// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using GitHubVulnerabilities2v3.Configuration;
using GitHubVulnerabilities2v3.Extensions;
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
using NuGetGallery;

namespace GitHubVulnerabilities2v3
{
    public class Job : JsonConfigurationJob, IDisposable
    {
        private readonly HttpClient _client = new HttpClient();

        public override async Task Run()
        {
            var collector = _serviceProvider.GetRequiredService<IAdvisoryCollector>();
            await collector.ProcessAsync(CancellationToken.None);
        }

        private async Task<RunMode> DetermineRunMode(ReadWriteCursor<DateTimeOffset> cursor)
        {
            var mode = RunMode.Update;
            await cursor.Load(CancellationToken.None);
            if (DateTimeOffset.Compare(cursor.Value.AddDays(30), DateTimeOffset.Now) >= 0)
            {
                cursor.Value = DateTimeOffset.FromUnixTimeSeconds(0);
                mode = RunMode.Regenerate;
            }
            await cursor.Save(CancellationToken.None);
            return mode;
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
                .RegisterType<PackageVulnerabilitiesManagementService>()
                .As<IPackageVulnerabilitiesManagementService>();

            containerBuilder
                .RegisterType<GitHubVersionRangeParser>()
                .As<IGitHubVersionRangeParser>();

            containerBuilder
                .RegisterType<BlobStorageVulnerabilityWriter>()
                .As<IVulnerabilityWriter>();

            containerBuilder
                .RegisterType<AdvisoryIngestor>()
                .As<IAdvisoryIngestor>();
        }

        protected void ConfigureQueryServices(ContainerBuilder containerBuilder)
        {
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

            containerBuilder
                .Register(async ctx =>
                {
                    var cursor = ctx.Resolve<ReadWriteCursor<DateTimeOffset>>();
                    return await DetermineRunMode(cursor);
                })
                .As<RunMode>();
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
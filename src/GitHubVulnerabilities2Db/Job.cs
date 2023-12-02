﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using GitHubVulnerabilities2Db.Configuration;
using GitHubVulnerabilities2Db.Fakes;
using GitHubVulnerabilities2Db.Gallery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Services.Cursor;
using NuGet.Services.GitHub.Collector;
using NuGet.Services.GitHub.Configuration;
using NuGet.Services.GitHub.GraphQL;
using NuGet.Services.GitHub.Ingest;
using NuGet.Services.Storage;
using NuGetGallery;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Security;

namespace GitHubVulnerabilities2Db
{
    public class Job : JsonConfigurationJob, IDisposable
    {
        private readonly HttpClient _client = new HttpClient();

        public override async Task Run()
        {
            var collector = _serviceProvider.GetRequiredService<IAdvisoryCollector>();
            while (await collector.ProcessAsync(CancellationToken.None));
            
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<GitHubVulnerabilities2DbConfiguration>(services, configurationRoot);
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            containerBuilder
                .RegisterAdapter<IOptionsSnapshot<GitHubVulnerabilities2DbConfiguration>, GitHubVulnerabilities2DbConfiguration>(c => c.Value);
            containerBuilder
                .RegisterAdapter<IOptionsSnapshot<GitHubVulnerabilities2DbConfiguration>, GraphQLQueryConfiguration>(c => c.Value);

            ConfigureQueryServices(containerBuilder);
            ConfigureIngestionServices(containerBuilder);
            ConfigureCollectorServices(containerBuilder);
        }

        protected void ConfigureIngestionServices(ContainerBuilder containerBuilder)
        {
            ConfigureGalleryServices(containerBuilder);

            containerBuilder
                .RegisterType<PackageVulnerabilitiesManagementService>()
                .As<IPackageVulnerabilitiesManagementService>();

            containerBuilder
                .RegisterType<GalleryDbVulnerabilityWriter>()
                .As<IVulnerabilityWriter>();

            containerBuilder
                .RegisterType<GitHubVersionRangeParser>()
                .As<IGitHubVersionRangeParser>();

            containerBuilder
                .RegisterType<AdvisoryIngestor>()
                .As<IAdvisoryIngestor>();
        }

        protected void ConfigureGalleryServices(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .Register(ctx =>
                {
                    var connection = CreateSqlConnection<GalleryDbConfiguration>();
                    return new EntitiesContext(connection, false);
                })
                .As<IEntitiesContext>();

            containerBuilder
                .RegisterGeneric(typeof(EntityRepository<>))
                .As(typeof(IEntityRepository<>));

            containerBuilder
                .RegisterType<ThrowingAuditingService>()
                .As<IAuditingService>();

            containerBuilder
                .RegisterType<ThrowingTelemetryService>()
                .As<ITelemetryService>();

            containerBuilder
                .RegisterType<ThrowingSecurityPolicyService>()
                .As<ISecurityPolicyService>();

            containerBuilder
                .RegisterType<FakeFeatureFlagService>()
                .As<IFeatureFlagService>();

            containerBuilder
                .RegisterType<PackageService>()
                .As<IPackageService>();

            containerBuilder
                .RegisterType<ThrowingIndexingService>()
                .As<IIndexingService>();

            containerBuilder
                .RegisterType<PackageUpdateService>()
                .As<IPackageUpdateService>();

            containerBuilder.RegisterType<AppConfiguration>()
                .As<IAppConfiguration>()
                .SingleInstance();

            var contentService = new FakeContentService();
            containerBuilder.RegisterInstance(contentService)
                .As<IContentService>()
                .SingleInstance();

            containerBuilder.RegisterType<ContentObjectService>()
                .As<IContentObjectService>()
                .SingleInstance();
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
                    var config = ctx.Resolve<GitHubVulnerabilities2DbConfiguration>();
                    return CloudStorageAccount.Parse(config.StorageConnectionString);
                })
                .As<CloudStorageAccount>();

            containerBuilder
                .RegisterType<AzureStorageFactory>()
                .WithParameter(
                    (parameter, ctx) => parameter.Name == "containerName",
                    (parameter, ctx) => ctx.Resolve<GitHubVulnerabilities2DbConfiguration>().CursorContainerName)
                .As<StorageFactory>()
                .As<IStorageFactory>();

            containerBuilder
                .Register(ctx => CreateCursor(ctx, config => config.AdvisoryCursorBlobName))
                .As<ReadWriteCursor<DateTimeOffset>>();

            containerBuilder
                .RegisterType<AdvisoryCollector>()
                .As<IAdvisoryCollector>();
        }

        private DurableCursor CreateCursor(IComponentContext ctx, Func<GitHubVulnerabilities2DbConfiguration, string> getBlobName)
        {
            var config = ctx.Resolve<IOptionsSnapshot<GitHubVulnerabilities2DbConfiguration>>().Value;
            var storageFactory = ctx.Resolve<IStorageFactory>();
            var storage = storageFactory.Create();
            return new DurableCursor(storage.ResolveUri(getBlobName(config)), storage, DateTimeOffset.MinValue);
        }
    }
}
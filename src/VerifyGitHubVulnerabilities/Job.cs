// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using GitHubVulnerabilities2Db.Collector;
using GitHubVulnerabilities2Db.GraphQL;
using GitHubVulnerabilities2Db.Ingest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGetGallery;
using VerifyGitHubVulnerabilities.Configuration;
using VerifyGitHubVulnerabilities.Verify;

namespace VerifyGitHubVulnerabilities
{
    class Job : JsonConfigurationJob
    {
        private readonly HttpClient _client = new HttpClient();

        public override async Task Run()
        {
            Console.Write("Fetching vulnerabilities from GitHub...");
            var advisoryQueryService = _serviceProvider.GetRequiredService<IAdvisoryQueryService>();
            var advisories = await advisoryQueryService.GetAdvisoriesSinceAsync(DateTimeOffset.MinValue, CancellationToken.None);
            Console.WriteLine($" FOUND {advisories.Count} advisories.");

            Console.WriteLine("Fetching vulnerabilities from DB...");
            var ingestor = _serviceProvider.GetRequiredService<IAdvisoryIngestor>();
            await ingestor.IngestAsync(advisories);

            var verifier = _serviceProvider.GetRequiredService<IPackageVulnerabilitiesVerifier>();
            Console.WriteLine(verifier.HasErrors ? 
                "DB does not match GitHub API - see stderr output for details" :
                "DB/metadata matches GitHub API!");
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<VerifyGitHubVulnerabilitiesConfiguration>(services, configurationRoot);
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .RegisterAdapter<IOptionsSnapshot<VerifyGitHubVulnerabilitiesConfiguration>, VerifyGitHubVulnerabilitiesConfiguration>(c => c.Value);

            ConfigureQueryServices(containerBuilder);
            ConfigureIngestionServices(containerBuilder);
        }

        protected void ConfigureIngestionServices(ContainerBuilder containerBuilder)
        {
            ConfigureGalleryServices(containerBuilder);

            containerBuilder
                .RegisterType<GitHubVersionRangeParser>()
                .As<IGitHubVersionRangeParser>();

            containerBuilder
                .RegisterType<AdvisoryIngestor>()
                .As<IAdvisoryIngestor>();

            containerBuilder
                .RegisterType<PackageVulnerabilitiesVerifier>()
                .As<IPackageVulnerabilitiesManagementService>()
                .As<IPackageVulnerabilitiesVerifier>()
                .SingleInstance();
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
       }

        protected void ConfigureQueryServices(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .RegisterInstance(_client)
                .As<HttpClient>();

            containerBuilder
                .RegisterGeneric(typeof(NullLogger<>))
                .As(typeof(ILogger<>));

            containerBuilder
                .RegisterType<VerifyGitHubVulnerabilities.GraphQL.QueryService>()
                .As<IQueryService>();

            containerBuilder
                .RegisterType<AdvisoryQueryBuilder>()
                .As<IAdvisoryQueryBuilder>();

            containerBuilder
                .RegisterType<AdvisoryQueryService>()
                .As<IAdvisoryQueryService>();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using NuGet.Services.GitHub.Collector;
using NuGet.Services.GitHub.GraphQL;
using NuGet.Services.GitHub.Ingest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        public override async Task Run()
        {
            var telemetryClient = _serviceProvider.GetRequiredService<NuGet.Services.Logging.ITelemetryClient>();

            Logger.LogInformation("Fetching vulnerabilities from GitHub...");
            var advisoryQueryService = _serviceProvider.GetRequiredService<IAdvisoryQueryService>();
            var advisories = await advisoryQueryService.GetAdvisoriesSinceAsync(DateTimeOffset.MinValue, CancellationToken.None);
            Logger.LogInformation("Found {Count} advisories.", advisories.Count);

            Logger.LogInformation("Fetching vulnerabilities from DB...");
            var ingestor = _serviceProvider.GetRequiredService<IAdvisoryIngestor>();
            await ingestor.IngestAsync(advisories.OrderBy(x => x.DatabaseId).ToList());

            var verifier = _serviceProvider.GetRequiredService<IPackageVulnerabilitiesVerifier>();
            if (verifier.HasErrors)
            {
                Logger.LogError("DB/metadata does not match GitHub API - see error logs for details");
                telemetryClient.TrackMetric(nameof(VerifyGitHubVulnerabilities) + ".DataIsInconsistent", 1);
            }
            else
            {
                Logger.LogInformation("DB/metadata matches GitHub API!");
                telemetryClient.TrackMetric(nameof(VerifyGitHubVulnerabilities) + ".DataIsConsistent", 1);
            }
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            ConfigureInitializationSection<VerifyGitHubVulnerabilitiesConfiguration>(services, configurationRoot);
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
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
                    return new EntitiesContext(connection, readOnly: true);
                })
                .As<IEntitiesContext>();

            containerBuilder
                .RegisterGeneric(typeof(EntityRepository<>))
                .As(typeof(IEntityRepository<>));
       }

        protected void ConfigureQueryServices(ContainerBuilder containerBuilder)
        {
            containerBuilder
                .RegisterInstance(new HttpClient())
                .As<HttpClient>();

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

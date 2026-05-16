// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using GitHubVulnerabilities2Db.Gallery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Services.Configuration;
using NuGet.Services.GitHub.Authentication;
using NuGet.Services.GitHub.Collector;
using NuGet.Services.GitHub.Configuration;
using NuGet.Services.GitHub.GraphQL;
using NuGet.Services.GitHub.Ingest;
using NuGet.Services.KeyVault;
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
            containerBuilder
                .RegisterAdapter<IOptionsSnapshot<VerifyGitHubVulnerabilitiesConfiguration>, GraphQLQueryConfiguration>(c => c.Value);

            ConfigureQueryServices(containerBuilder, configurationRoot);
            ConfigureIngestionServices(containerBuilder);
        }

        protected void ConfigureIngestionServices(ContainerBuilder containerBuilder)
        {
            ConfigureGalleryServices(containerBuilder);

            containerBuilder
                .RegisterType<GitHubVersionRangeParser>()
                .As<IGitHubVersionRangeParser>();

            containerBuilder
                .RegisterType<GalleryDbVulnerabilityWriter>()
                .As<IVulnerabilityWriter>();

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

        protected void ConfigureQueryServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            containerBuilder
                .RegisterInstance(new HttpClient())
                .As<HttpClient>();

            var keyVaultUseManagedIdentity = configurationRoot.GetValue<bool>(Constants.KeyVaultUseManagedIdentity, false);
            var keyVaultName = configurationRoot[Constants.KeyVaultVaultNameKey] ?? throw new InvalidOperationException("Key vault name is not configured.");
            var keyVaultManagedIdentityClientId = configurationRoot[Constants.ManagedIdentityClientIdKey];

            containerBuilder
                .Register(ctx =>
                {
                    var config = ctx.Resolve<VerifyGitHubVulnerabilitiesConfiguration>();
                    if (!keyVaultUseManagedIdentity)
                    {
                        throw new InvalidOperationException("Only managed identity authentication is supported.");
                    }
#if DEBUG
                    var credential = new DefaultAzureCredential();
#else
                    if (string.IsNullOrWhiteSpace(keyVaultManagedIdentityClientId))
                    {
                        throw new InvalidOperationException("Managed identity client ID is not configured.");
                    }
                    var credential = new ManagedIdentityCredential(keyVaultManagedIdentityClientId);
#endif
                    string vaultName = keyVaultName.ToLowerInvariant();
                    string keyName = config.GitHubAppPrivateKeyName.ToLowerInvariant();

                    var keyUri = new Uri($"https://{vaultName}.vault.azure.net/keys/{keyName}");
                    CryptographyClient cryptographyClient = new CryptographyClient(keyUri, credential);
                    return new KeyVaultDataSigner(cryptographyClient);
                })
                .As<IKeyVaultDataSigner>();

            containerBuilder
                .RegisterType<GitHubPersonalAccessTokenAuthProvider>()
                .AsSelf()
                .SingleInstance();

            containerBuilder
                .RegisterType<GitHubAppAuthProvider>()
                .AsSelf()
                .SingleInstance();

            containerBuilder
                .Register<IGitHubAuthProvider>(ctx => {
                    var config = ctx.Resolve<VerifyGitHubVulnerabilitiesConfiguration>();
                    if (string.IsNullOrWhiteSpace(config.GitHubAppId))
                    {
                        return ctx.Resolve<GitHubPersonalAccessTokenAuthProvider>();
                    }
                    return ctx.Resolve<GitHubAppAuthProvider>();
                })
                .As<IGitHubAuthProvider>()
                .SingleInstance();

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
    }
}

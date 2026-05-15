// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using GitHubVulnerabilities2Db.Configuration;
using GitHubVulnerabilities2Db.Fakes;
using GitHubVulnerabilities2Db.Gallery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;
using NuGet.Services.Cursor;
using NuGet.Services.GitHub.Authentication;
using NuGet.Services.GitHub.Collector;
using NuGet.Services.GitHub.Configuration;
using NuGet.Services.GitHub.GraphQL;
using NuGet.Services.GitHub.Ingest;
using NuGet.Services.KeyVault;
using NuGet.Services.Storage;
using NuGetGallery;
using NuGetGallery.Auditing;
using NuGetGallery.Configuration;
using NuGetGallery.Security;
using Constants = NuGet.Services.Configuration.Constants;

namespace GitHubVulnerabilities2Db
{
    public class Job : JsonConfigurationJob, IDisposable
    {
        private const string ManagedIdentityClientIdKey = "UserManagedIdentityClientId";
        private readonly HttpClient _client = new HttpClient();

        public override async Task Run()
        {
            var collector = _serviceProvider.GetRequiredService<IAdvisoryCollector>();
            while (await collector.ProcessAsync(CancellationToken.None)) ;

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

            ConfigureQueryServices(containerBuilder, configurationRoot);
            ConfigureIngestionServices(containerBuilder);
            ConfigureCollectorServices(containerBuilder, configurationRoot);
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

        protected void ConfigureQueryServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            containerBuilder
                .RegisterInstance(_client)
                .As<HttpClient>()
                .ExternallyOwned(); // We don't want autofac disposing this--see https://github.com/NuGet/NuGetGallery/issues/9194

            var keyVaultUseManagedIdentity = configurationRoot.GetValue<bool>(Constants.KeyVaultUseManagedIdentity, false);
            var keyVaultName = configurationRoot[Constants.KeyVaultVaultNameKey] ?? throw new InvalidOperationException("Key vault name is not configured.");

            containerBuilder
                .Register(ctx =>
                {
                    var config = ctx.Resolve<GitHubVulnerabilities2DbConfiguration>();
                    if (!keyVaultUseManagedIdentity)
                    {
                        throw new InvalidOperationException("Only managed identity authentication is supported.");
                    }
#if DEBUG
                    var credential = new DefaultAzureCredential();
#else
                    if (string.IsNullOrWhiteSpace(keyVaultConfig.ClientId))
                    {
                        throw new InvalidOperationException("Key vault client ID is not configured.");
                    }
                    var credential = new ManagedIdentityCredential(keyVaultConfig.ClientId);
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
                    var config = ctx.Resolve<GitHubVulnerabilities2DbConfiguration>();
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

        protected void ConfigureCollectorServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            containerBuilder
                .Register(ctx =>
                {
                    var config = ctx.Resolve<GitHubVulnerabilities2DbConfiguration>();
#if DEBUG
                    var credential = new DefaultAzureCredential();
#else
                    var credential = new ManagedIdentityCredential(configurationRoot[ManagedIdentityClientIdKey]);
#endif
                    return new BlobServiceClientFactory(new Uri(config.StorageConnectionString), credential);
                })
                .As<BlobServiceClientFactory>();

            containerBuilder
                .Register(ctx =>
                {
                    return new AzureStorageFactory(
                        ctx.Resolve<BlobServiceClientFactory>(),
                        ctx.Resolve<GitHubVulnerabilities2DbConfiguration>().CursorContainerName,
                        enablePublicAccess: true,
                        ctx.Resolve<ILogger<AzureStorage>>());
                })
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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Storage.Blobs;
using GitHubVulnerabilities2v3.Configuration;
using GitHubVulnerabilities2v3.Extensions;
using GitHubVulnerabilities2v3.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Jobs;
using NuGet.Services.Configuration;
using NuGet.Services.Cursor;
using NuGet.Services.GitHub.Authentication;
using NuGet.Services.GitHub.Collector;
using NuGet.Services.GitHub.Configuration;
using NuGet.Services.GitHub.GraphQL;
using NuGet.Services.GitHub.Ingest;
using NuGet.Services.KeyVault;
using NuGet.Services.Storage;

namespace GitHubVulnerabilities2v3
{
    public class Job : JsonConfigurationJob, IDisposable
    {
        private readonly HttpClient _client = new HttpClient();
        private readonly ProductInfoHeaderValue _userAgent = new ProductInfoHeaderValue("NuGet.Jobs.GitHubVulnerabilities2v3", "1.0.0");

        private const string PrimaryStorageKey = "PrimaryStorage";
        private const string SecondaryStorageKey = "SecondaryStorage";
        private static readonly string[] DestinationKeys = [PrimaryStorageKey, SecondaryStorageKey];
        private const bool ContainerPublicAccess = true;

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

            ConfigureQueryServices(containerBuilder, configurationRoot);
            ConfigureIngestionServices(containerBuilder);
            ConfigureCollectorServices(containerBuilder, configurationRoot);
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

        protected void ConfigureQueryServices(ContainerBuilder containerBuilder, IConfigurationRoot configurationRoot)
        {
            _client.DefaultRequestHeaders.UserAgent.Add(_userAgent);
            containerBuilder
                .RegisterInstance(_client)
                .As<HttpClient>()
                .ExternallyOwned(); // We don't want autofac disposing this--see https://github.com/NuGet/NuGetGallery/issues/9194

            var keyVaultUseManagedIdentity = configurationRoot.GetValue<bool>(Constants.KeyVaultUseManagedIdentity, false);
            var keyVaultName = configurationRoot[Constants.KeyVaultVaultNameKey] ?? throw new InvalidOperationException("Key vault name is not configured.");
            var keyVaultManagedIdentityClientId = configurationRoot[Constants.ManagedIdentityClientIdKey];

            containerBuilder
                .Register(ctx =>
                {
                    var config = ctx.Resolve<GitHubVulnerabilities2v3Configuration>();
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
                    var config = ctx.Resolve<GitHubVulnerabilities2v3Configuration>();
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
            var numDestinations = HasSecondaryStorage(configurationRoot) ? 2 : 1;

            for (int i = 0; i < numDestinations; ++i)
            {
                var index = i; // Adding 
                var storageKey = DestinationKeys[index];

                containerBuilder
                    .Register(ctx =>
                    {
                        var config = ctx.Resolve<GitHubVulnerabilities2v3Configuration>();
#if DEBUG
                        var credential = new DefaultAzureCredential();
#else
                        var credential = new ManagedIdentityCredential(configurationRoot[Constants.ManagedIdentityClientIdKey]);
#endif
                        return new BlobServiceClientFactory(new Uri(config.Destinations[index].StorageConnectionString), credential);
                    })
                    .Keyed<BlobServiceClientFactory>(storageKey);

                containerBuilder
                    .Register(ctx =>
                    {
                        return new AzureStorageFactory(
                            ctx.ResolveKeyed<BlobServiceClientFactory>(storageKey),
                            ctx.Resolve<GitHubVulnerabilities2v3Configuration>().V3VulnerabilityContainerName,
                            enablePublicAccess: ContainerPublicAccess,
                            azureStorageLogger: ctx.Resolve<ILogger<AzureStorage>>());
                    })
                    .As<IStorageFactory>()
                    .PreserveExistingDefaults(); // the first registration will be "default", i.e. will resolve if just IStorageFactory is requested from DI container
            }

            containerBuilder
                .Register(ctx => CreateCursor(ctx, config => config.AdvisoryCursorBlobName))
                .As<ReadWriteCursor<DateTimeOffset>>();

            containerBuilder
                .RegisterType<AdvisoryCollector>()
                .As<IAdvisoryCollector>();
        }

        private static bool HasSecondaryStorage(IConfigurationRoot configurationRoot)
        {
            return configurationRoot.GetSection("Initialization:Destinations:1").Exists();
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

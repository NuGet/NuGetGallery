// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Autofac;
using Autofac.Builder;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Configuration;
using NuGet.Services.Configuration;
using NuGet.Services.GitHub.Authentication;
using NuGet.Services.KeyVault;

namespace NuGet.Services.GitHub.Configuration
{
    public static class ConfigurationExtensions
    {
        public static IRegistrationBuilder<KeyVaultDataSigner, SimpleActivatorData, SingleRegistrationStyle>
            RegisterKeyVaultDataSigner<TConfiguration>(this ContainerBuilder builder, IConfigurationRoot configurationRoot)
            where TConfiguration: GraphQLQueryConfiguration
        {
            var keyVaultUseManagedIdentity = configurationRoot.GetValue<bool>(Constants.KeyVaultUseManagedIdentity, false);
            var keyVaultName = configurationRoot[Constants.KeyVaultVaultNameKey] ?? throw new InvalidOperationException("Key vault name is not configured.");
            var keyVaultManagedIdentityClientId = string.IsNullOrWhiteSpace(configurationRoot[Constants.KeyVaultClientIdKey]) ? configurationRoot[Constants.ManagedIdentityClientIdKey] : configurationRoot[Constants.KeyVaultClientIdKey];

            return builder.Register(ctx =>
            {
                var config = ctx.Resolve<TConfiguration>();
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
            });
        }

        public static IRegistrationBuilder<IGitHubAuthProvider, SimpleActivatorData, SingleRegistrationStyle>
            RegisterGitHubAuthProvider<TConfiguration>(this ContainerBuilder builder)
            where TConfiguration : GraphQLQueryConfiguration
        {
            builder
                .RegisterType<GitHubPersonalAccessTokenAuthProvider>()
                .AsSelf()
                .SingleInstance();

            builder
                .RegisterType<GitHubAppAuthProvider>()
                .AsSelf()
                .SingleInstance();

            return builder
                .Register<IGitHubAuthProvider>(ctx => {
                    var config = ctx.Resolve<TConfiguration>();
                    if (string.IsNullOrWhiteSpace(config.GitHubAppId))
                    {
                        return ctx.Resolve<GitHubPersonalAccessTokenAuthProvider>();
                    }
                    return ctx.Resolve<GitHubAppAuthProvider>();
                });
        }
    }
}

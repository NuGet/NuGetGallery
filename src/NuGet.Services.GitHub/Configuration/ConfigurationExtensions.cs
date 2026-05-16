using System;
using Autofac;
using Autofac.Builder;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Configuration;
using NuGet.Services.Configuration;
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
            var keyVaultManagedIdentityClientId = configurationRoot[Constants.ManagedIdentityClientIdKey];

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
    }
}

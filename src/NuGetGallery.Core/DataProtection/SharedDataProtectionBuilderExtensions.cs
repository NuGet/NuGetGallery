// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Core.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Services.KeyVault;

namespace NuGetGallery.DataProtection
{
    public static class SharedDataProtectionBuilderExtensions
    {
        public static IDataProtectionBuilder ConfigureSharedDataProtection(
            this IDataProtectionBuilder builder,
            IXmlRepository repository,
            SharedDataProtectionConfiguration configuration,
            IKeyEncryptionKeyResolver keyEncryptionKeyResolver,
            bool isProduction)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            configuration.Validate(isProduction);

            if (configuration.EncryptKeysAtRest)
            {
                if (keyEncryptionKeyResolver == null)
                {
                    throw new InvalidOperationException("A Key Vault key resolver is required when encryption at rest is enabled.");
                }

                if (keyEncryptionKeyResolver is IKeyEncryptionKeyMetadataValidator metadataValidator)
                {
                    metadataValidator.ValidateKey(configuration.KeyVaultKeyIdentifier);
                }
                else if (isProduction)
                {
                    throw new InvalidOperationException("Production Data Protection requires a Key Vault resolver that validates key metadata at startup.");
                }
            }

            builder
                .SetApplicationName(configuration.ApplicationDiscriminator)
                .SetDefaultKeyLifetime(configuration.KeyLifetime);
            builder.Services.Configure<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(
                options => options.XmlRepository = repository);

            if (configuration.EncryptKeysAtRest)
            {
                builder.ProtectKeysWithAzureKeyVault(
                    configuration.KeyVaultKeyIdentifier,
                    keyEncryptionKeyResolver);
            }

            return builder;
        }

        public static IDataProtectionBuilder ConfigureSharedDataProtection(
            this IDataProtectionBuilder builder,
            IXmlRepository repository,
            SharedDataProtectionConfiguration configuration,
            KeyVaultConfiguration keyVaultConfiguration,
            bool isProduction)
        {
            IKeyEncryptionKeyResolver resolver = configuration?.EncryptKeysAtRest == true
                ? new KeyVaultKeyEncryptionKeyResolver(keyVaultConfiguration)
                : null;

            return builder.ConfigureSharedDataProtection(
                repository,
                configuration,
                resolver,
                isProduction);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.KeyVault;

namespace NuGetGallery.Configuration.SecretReader
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        internal const string KeyVaultConfigurationPrefix = "KeyVault.";

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            if (secretReader == null)
            {
                throw new ArgumentNullException(nameof(secretReader));
            }

            return new SecretInjector(secretReader);
        }

        public ISecretReader CreateSecretReader(IGalleryConfigurationService configurationService)
        {
            if (configurationService == null)
            {
                throw new ArgumentNullException(nameof(configurationService));
            }

            ISecretReader secretReader;

            // Create an empty instance of KeyVault configuration, and later initialize it.
            const string placeholder = "placeholder";
            var keyVaultConfiguration = new KeyVaultConfiguration(placeholder, placeholder, placeholder, validateCertificate: true);
            keyVaultConfiguration = configurationService.ResolveConfigObject(keyVaultConfiguration, KeyVaultConfigurationPrefix).Result;

            if (!string.IsNullOrEmpty(keyVaultConfiguration.VaultName) && keyVaultConfiguration.VaultName != placeholder)
            {
                secretReader = new KeyVaultReader(keyVaultConfiguration);
            }
            else
            {
                secretReader = new EmptySecretReader();
            }

            return secretReader;
        }
    }
}
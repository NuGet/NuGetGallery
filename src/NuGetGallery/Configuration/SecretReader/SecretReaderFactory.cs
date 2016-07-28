// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.KeyVault;

namespace NuGetGallery.Configuration.SecretReader
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        internal const string KeyVaultConfigurationPrefix = "KeyVault.";
        internal const string VaultNameConfigurationKey = "VaultName";
        internal const string ClientIdConfigurationKey = "ClientId";
        internal const string CertificateThumbprintConfigurationKey = "CertificateThumbprint";

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

            var vaultName = configurationService.ReadSetting($"{KeyVaultConfigurationPrefix}{VaultNameConfigurationKey}").Result;

            if (!string.IsNullOrEmpty(vaultName))
            {
                var clientId = configurationService.ReadSetting($"{KeyVaultConfigurationPrefix}{ClientIdConfigurationKey}").Result;
                var certificateThumbprint = configurationService.ReadSetting($"{KeyVaultConfigurationPrefix}{CertificateThumbprintConfigurationKey}").Result;

                var keyVaultConfiguration = new KeyVaultConfiguration(vaultName, clientId, certificateThumbprint, validateCertificate: true);

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
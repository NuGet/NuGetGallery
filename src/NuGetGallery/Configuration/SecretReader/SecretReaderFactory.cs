// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Services.KeyVault;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Configuration.SecretReader
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        internal const string KeyVaultConfigurationPrefix = "KeyVault.";
        internal const string VaultNameConfigurationKey = "VaultName";
        internal const string ClientIdConfigurationKey = "ClientId";
        internal const string CertificateThumbprintConfigurationKey = "CertificateThumbprint";
        internal const string CacheRefreshInterval = "CacheRefreshInterval";
        private IDiagnosticsService _diagnosticsService;

        public SecretReaderFactory(IDiagnosticsService diagnosticsService)
        {
            if (diagnosticsService == null)
            {
                throw new ArgumentNullException(nameof(diagnosticsService));
            }

            _diagnosticsService = diagnosticsService;
        }

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

            var vaultName = configurationService.ReadSetting(
                string.Format(CultureInfo.InvariantCulture, "{0}{1}", KeyVaultConfigurationPrefix, VaultNameConfigurationKey)).Result;

            if (!string.IsNullOrEmpty(vaultName))
            {
                var clientId = configurationService.ReadSetting(
                    string.Format(CultureInfo.InvariantCulture, "{0}{1}", KeyVaultConfigurationPrefix, ClientIdConfigurationKey)).Result;
                var certificateThumbprint = configurationService.ReadSetting(
                    string.Format(CultureInfo.InvariantCulture, "{0}{1}", KeyVaultConfigurationPrefix, CertificateThumbprintConfigurationKey)).Result;

                var keyVaultConfiguration = new KeyVaultConfiguration(vaultName, clientId, certificateThumbprint, validateCertificate: true);

                secretReader = new KeyVaultReader(keyVaultConfiguration);
            }
            else
            {
                secretReader = new EmptySecretReader();
            }

            int cacheRefreshIntervalSeconds;
            try
            {
                cacheRefreshIntervalSeconds = int.Parse(configurationService.ReadSetting(
                    string.Format(CultureInfo.InvariantCulture, "{0}{1}", KeyVaultConfigurationPrefix, CacheRefreshInterval)).Result);
            }
            catch (Exception)
            {
                cacheRefreshIntervalSeconds = 60 * 60 * 24; // one day in seconds
            }

            return new CachingSecretReader(secretReader, _diagnosticsService, cacheRefreshIntervalSeconds);
        }
    }
}
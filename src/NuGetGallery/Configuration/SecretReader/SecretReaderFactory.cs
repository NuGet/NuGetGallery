// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;
using NuGetGallery.Diagnostics;

namespace NuGetGallery.Configuration.SecretReader
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        internal const string KeyVaultConfigurationPrefix = "KeyVault.";
        internal const string VaultNameConfigurationKey = "VaultName";
        internal const string ClientIdConfigurationKey = "ClientId";
        internal const string StoreNameKey = "StoreName";
        internal const string StoreLocationKey = "StoreLocation";
        internal const string CertificateThumbprintConfigurationKey = "CertificateThumbprint";
        internal const string CacheRefreshInterval = "CacheRefreshIntervalSec";

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            if (secretReader == null)
            {
                throw new ArgumentNullException(nameof(secretReader));
            }

            return new SecretInjector(secretReader);
        }

        private static Task<string> ReadKeyVaultSetting(IGalleryConfigurationService configService, string key)
        {
            return configService.ReadSetting(
                string.Format(CultureInfo.InvariantCulture, "{0}{1}", KeyVaultConfigurationPrefix,
                    key));
        }

        private async Task<ISecretReader> CreateSecretReaderAsync()
        {
            var configService = new ConfigurationService(new EmptySecretReaderFactory());

            ISecretReader secretReader;

            var vaultName = await ReadKeyVaultSetting(configService, VaultNameConfigurationKey);

            if (!string.IsNullOrEmpty(vaultName))
            {
                var clientId = await ReadKeyVaultSetting(configService, ClientIdConfigurationKey);
                var certificateThumbprint =
                    await ReadKeyVaultSetting(configService, CertificateThumbprintConfigurationKey);

                var storeNameString = await ReadKeyVaultSetting(configService, StoreNameKey);
                var storeName = string.IsNullOrEmpty(storeNameString)
                    ? ConfigurationUtility.ConvertFromString<StoreName>(storeNameString)
                    : StoreName.My;

                var storeLocationString = await ReadKeyVaultSetting(configService, StoreLocationKey);
                var storeLocation = string.IsNullOrEmpty(storeLocationString)
                    ? ConfigurationUtility.ConvertFromString<StoreLocation>(storeLocationString)
                    : StoreLocation.LocalMachine;

                var keyVaultConfiguration = new KeyVaultConfiguration(vaultName, clientId, certificateThumbprint,
                    storeName, storeLocation, validateCertificate: true);

                secretReader = new KeyVaultReader(keyVaultConfiguration);
            }
            else
            {
                secretReader = new EmptySecretReader();
            }

            int cacheRefreshIntervalSeconds;
            try
            {
                var refreshIntervalString = await ReadKeyVaultSetting(configService, CacheRefreshInterval);

                cacheRefreshIntervalSeconds = string.IsNullOrEmpty(refreshIntervalString)
                    ? CachingSecretReader.DefaultRefreshIntervalSec
                    : int.Parse(refreshIntervalString);
            }
            catch (Exception)
            {
                cacheRefreshIntervalSeconds = 60 * 60 * 24; // one day in seconds
            }

            return new CachingSecretReader(secretReader, cacheRefreshIntervalSeconds);
        }

        public ISecretReader CreateSecretReader()
        {
            // It is ok to call ".Result" here because we are initializing.
            // You should NEVER call ".Result" on a method that accesses KeyVault in normal circumstances.
            return CreateSecretReaderAsync().Result;
        }
    }
}
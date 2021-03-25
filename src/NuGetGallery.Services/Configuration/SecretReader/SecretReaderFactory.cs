// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Web.Hosting;
using Microsoft.Extensions.Logging;
using NuGet.Services.KeyVault;

namespace NuGetGallery.Configuration.SecretReader
{
    public class SecretReaderFactory
    {
        internal const string KeyVaultConfigurationPrefix = "KeyVault.";
        internal const string UseManagedIdentityConfigurationKey = "UseManagedIdentity";
        internal const string VaultNameConfigurationKey = "VaultName";
        internal const string ClientIdConfigurationKey = "ClientId";
        internal const string CertificateThumbprintConfigurationKey = "CertificateThumbprint";
        internal const string CertificateStoreLocation = "StoreLocation";
        internal const string CertificateStoreName = "StoreName";

        private const int SecretCachingRefreshInterval = 60 * 60 * 6;
        private IGalleryConfigurationService _configurationService;

        public SecretReaderFactory(IGalleryConfigurationService configurationService)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        }

        public ISyncSecretInjector CreateSecretInjector(ISyncSecretReader secretReader)
        {
            if (secretReader == null)
            {
                throw new ArgumentNullException(nameof(secretReader));
            }

            return new SyncSecretInjector(secretReader);
        }

        private string ResolveKeyVaultSettingName(string settingName)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}{1}", KeyVaultConfigurationPrefix, settingName);
        }

        public CachingBackgroundRefreshingSecretReader CreateSecretReader(
            ICollection<string> secretNames,
            ILogger logger,
            ICachingBackgroundRefreshingSecretReaderTelemetryService telemetryService)
        {
            ISecretReader secretReader;

            var vaultName = _configurationService.ReadRawSetting(ResolveKeyVaultSettingName(VaultNameConfigurationKey));

            if (!string.IsNullOrEmpty(vaultName))
            {
                var useManagedIdentity = GetOptionalKeyVaultBoolSettingValue(UseManagedIdentityConfigurationKey, defaultValue: false);

                KeyVaultConfiguration keyVaultConfiguration;
                if (useManagedIdentity)
                {
                    keyVaultConfiguration = new KeyVaultConfiguration(vaultName);
                }
                else
                {
                    var clientId = _configurationService.ReadRawSetting(ResolveKeyVaultSettingName(ClientIdConfigurationKey));
                    var certificateThumbprint = _configurationService.ReadRawSetting(ResolveKeyVaultSettingName(CertificateThumbprintConfigurationKey));
                    var storeName = GetOptionalKeyVaultEnumSettingValue(CertificateStoreName, StoreName.My);
                    var storeLocation = GetOptionalKeyVaultEnumSettingValue(CertificateStoreLocation, StoreLocation.LocalMachine);
                    var certificate = CertificateUtility.FindCertificateByThumbprint(storeName, storeLocation, certificateThumbprint, validationRequired: true);
                    keyVaultConfiguration = new KeyVaultConfiguration(vaultName, clientId, certificate);
                }

                secretReader = new KeyVaultReader(keyVaultConfiguration);
            }
            else
            {
                secretReader = new EmptySecretReader();
            }

            return new CachingBackgroundRefreshingSecretReader(
                secretReader,
                HostingEnvironment.QueueBackgroundWorkItem,
                secretNames,
                telemetryService,
                logger,
                refreshInterval: TimeSpan.FromDays(1),
                backgroundTaskSleepInterval: TimeSpan.FromMinutes(1));
        }

        private bool GetOptionalKeyVaultBoolSettingValue(string settingName, bool defaultValue)
        {
            var settingValueStr = _configurationService.ReadRawSetting(ResolveKeyVaultSettingName(settingName));
            if (!bool.TryParse(settingValueStr, out var settingValue))
            {
                return defaultValue;
            }

            return settingValue;
        }

        private TEnum GetOptionalKeyVaultEnumSettingValue<TEnum>(string settingName, TEnum defaultValue)
            where TEnum: struct
        {
            var settingValueStr = _configurationService.ReadRawSetting(ResolveKeyVaultSettingName(settingName));
            if (!Enum.TryParse<TEnum>(settingValueStr, out var settingValue))
            {
                return defaultValue;
            }

            return settingValue;
        }
    }
}
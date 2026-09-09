// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery.DataProtection;

namespace NuGetGallery.Configuration
{
    public static class SharedDataProtectionConfigurationExtensions
    {
        public static SharedDataProtectionConfiguration ToSharedDataProtectionConfiguration(
            this IAppConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            string storageType;
            string storageLocation;
            switch (configuration.StorageType)
            {
                case NuGetGallery.Configuration.StorageType.AzureStorage:
                    storageType = DataProtectionStorageType.AzureStorage;
                    storageLocation = configuration.AzureStorage_DataProtection_ConnectionString;
                    break;

                case NuGetGallery.Configuration.StorageType.FileSystem:
                case NuGetGallery.Configuration.StorageType.NotSpecified:
                    storageType = DataProtectionStorageType.FileSystem;
                    storageLocation = configuration.FileStorageDirectory;
                    break;

                default:
                    storageType = configuration.StorageType;
                    storageLocation = null;
                    break;
            }

            return new SharedDataProtectionConfiguration
            {
                StorageType = storageType,
                StorageLocation = storageLocation,
                ApplicationDiscriminator = configuration.DataProtectionApplicationDiscriminator,
                KeyLifetime = configuration.DataProtectionKeyLifetime,
                EncryptKeysAtRest = configuration.DataProtectionEncryptKeysAtRest,
                KeyVaultKeyIdentifier = configuration.DataProtectionKeyVaultKeyIdentifier,
                KeyVaultKeyRotationPeriod = configuration.DataProtectionKeyVaultKeyRotationPeriod,
                KeyRingRetentionPeriod = configuration.DataProtectionKeyRingRetentionPeriod,
                KeyVaultKeyRetentionPeriod = configuration.DataProtectionKeyVaultKeyRetentionPeriod,
            };
        }
    }
}

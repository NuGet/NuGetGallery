// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGetGallery.Authentication;
using NuGetGallery.Configuration;
using NuGetGallery.DataProtection;
using Xunit;

namespace NuGetGallery
{
    public class SharedDataProtectionConfigurationExtensionsFacts
    {
        [Fact]
        public void MapsAzureStorageAndProtectionSettings()
        {
            Mock<IAppConfiguration> legacy = CreateConfiguration();
            legacy.Setup(x => x.StorageType).Returns(Configuration.StorageType.AzureStorage);
            legacy.Setup(x => x.AzureStorage_DataProtection_ConnectionString).Returns("azure-location");

            SharedDataProtectionConfiguration result =
                legacy.Object.ToSharedDataProtectionConfiguration();

            Assert.Equal(DataProtectionStorageType.AzureStorage, result.StorageType);
            Assert.Equal("azure-location", result.StorageLocation);
            AssertEquivalentSettings(legacy.Object, result);
        }

        [Theory]
        [InlineData(Configuration.StorageType.FileSystem)]
        [InlineData(Configuration.StorageType.NotSpecified)]
        public void MapsFileSystemStorageAndProtectionSettings(string storageType)
        {
            Mock<IAppConfiguration> legacy = CreateConfiguration();
            legacy.Setup(x => x.StorageType).Returns(storageType);
            legacy.Setup(x => x.FileStorageDirectory).Returns("filesystem-location");

            SharedDataProtectionConfiguration result =
                legacy.Object.ToSharedDataProtectionConfiguration();

            Assert.Equal(DataProtectionStorageType.FileSystem, result.StorageType);
            Assert.Equal("filesystem-location", result.StorageLocation);
            AssertEquivalentSettings(legacy.Object, result);
        }

        private static Mock<IAppConfiguration> CreateConfiguration()
        {
            var configuration = new Mock<IAppConfiguration>();
            configuration
                .Setup(x => x.DataProtectionApplicationDiscriminator)
                .Returns(SharedCookieConstants.DataProtectionApplicationName);
            configuration.Setup(x => x.DataProtectionKeyLifetime).Returns(TimeSpan.FromDays(90));
            configuration.Setup(x => x.DataProtectionEncryptKeysAtRest).Returns(true);
            configuration
                .Setup(x => x.DataProtectionKeyVaultKeyIdentifier)
                .Returns("https://vault.vault.azure.net/keys/data-protection");
            configuration
                .Setup(x => x.DataProtectionKeyVaultKeyRotationPeriod)
                .Returns(TimeSpan.FromDays(30));
            configuration
                .Setup(x => x.DataProtectionKeyRingRetentionPeriod)
                .Returns(TimeSpan.FromDays(365));
            configuration
                .Setup(x => x.DataProtectionKeyVaultKeyRetentionPeriod)
                .Returns(TimeSpan.FromDays(730));
            return configuration;
        }

        private static void AssertEquivalentSettings(
            IAppConfiguration legacy,
            SharedDataProtectionConfiguration modern)
        {
            Assert.Equal(legacy.DataProtectionApplicationDiscriminator, modern.ApplicationDiscriminator);
            Assert.Equal(legacy.DataProtectionKeyLifetime, modern.KeyLifetime);
            Assert.Equal(legacy.DataProtectionEncryptKeysAtRest, modern.EncryptKeysAtRest);
            Assert.Equal(legacy.DataProtectionKeyVaultKeyIdentifier, modern.KeyVaultKeyIdentifier);
            Assert.Equal(legacy.DataProtectionKeyVaultKeyRotationPeriod, modern.KeyVaultKeyRotationPeriod);
            Assert.Equal(legacy.DataProtectionKeyRingRetentionPeriod, modern.KeyRingRetentionPeriod);
            Assert.Equal(legacy.DataProtectionKeyVaultKeyRetentionPeriod, modern.KeyVaultKeyRetentionPeriod);
        }
    }
}

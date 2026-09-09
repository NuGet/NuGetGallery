// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGetGallery.Authentication;
using NuGetGallery.DataProtection;
using Xunit;

namespace NuGetGallery
{
    public class SharedDataProtectionConfigurationFacts
    {
        [Fact]
        public void ValidProductionConfigurationIsAccepted()
        {
            SharedDataProtectionConfiguration target = CreateValidConfiguration();

            target.Validate(isProduction: true);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("unknown")]
        public void InvalidStorageTypeIsRejected(string storageType)
        {
            SharedDataProtectionConfiguration target = CreateValidConfiguration();
            target.StorageType = storageType;

            Assert.Throws<InvalidOperationException>(() => target.Validate(isProduction: true));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("$$secret$$")]
        public void MissingOrUnresolvedStorageLocationIsRejected(string location)
        {
            SharedDataProtectionConfiguration target = CreateValidConfiguration();
            target.StorageLocation = location;

            Assert.Throws<InvalidOperationException>(() => target.Validate(isProduction: true));
        }

        [Fact]
        public void MalformedAzureStorageLocationIsRejected()
        {
            SharedDataProtectionConfiguration target = CreateValidConfiguration();
            target.StorageLocation = "not-a-connection-string";

            Assert.Throws<InvalidOperationException>(() => target.Validate(isProduction: true));
        }

        [Fact]
        public void WrongApplicationDiscriminatorIsRejected()
        {
            SharedDataProtectionConfiguration target = CreateValidConfiguration();
            target.ApplicationDiscriminator = "OtherApplication";

            Assert.Throws<InvalidOperationException>(() => target.Validate(isProduction: true));
        }

        [Fact]
        public void ProductionPlaintextIsRejected()
        {
            SharedDataProtectionConfiguration target = CreateValidConfiguration();
            target.EncryptKeysAtRest = false;
            target.KeyVaultKeyIdentifier = null;

            Assert.Throws<InvalidOperationException>(() => target.Validate(isProduction: true));
            target.Validate(isProduction: false);
        }

        [Theory]
        [InlineData("http://vault.vault.azure.net/keys/data-protection")]
        [InlineData("https://vault.vault.azure.net/keys/data-protection/version")]
        [InlineData("https://vault.vault.azure.net/secrets/data-protection")]
        [InlineData("https://vault.vault.azure.net/keys/data-protection?version=1")]
        public void InvalidOrVersionedKeyIdentifiersAreRejected(string identifier)
        {
            SharedDataProtectionConfiguration target = CreateValidConfiguration();
            target.KeyVaultKeyIdentifier = identifier;

            Assert.Throws<InvalidOperationException>(() => target.Validate(isProduction: true));
        }

        [Fact]
        public void RotationMustBeShorterThanKeyLifetime()
        {
            SharedDataProtectionConfiguration target = CreateValidConfiguration();
            target.KeyVaultKeyRotationPeriod = target.KeyLifetime;

            Assert.Throws<InvalidOperationException>(() => target.Validate(isProduction: true));
        }

        [Fact]
        public void KeyVaultVersionsMustOutliveRetainedRingEntries()
        {
            SharedDataProtectionConfiguration target = CreateValidConfiguration();
            target.KeyVaultKeyRetentionPeriod = target.KeyRingRetentionPeriod - TimeSpan.FromDays(1);

            Assert.Throws<InvalidOperationException>(() => target.Validate(isProduction: true));
        }

        private static SharedDataProtectionConfiguration CreateValidConfiguration()
        {
            return new SharedDataProtectionConfiguration
            {
                StorageType = DataProtectionStorageType.AzureStorage,
                StorageLocation = "UseDevelopmentStorage=true",
                ApplicationDiscriminator = SharedCookieConstants.DataProtectionApplicationName,
                KeyLifetime = TimeSpan.FromDays(90),
                EncryptKeysAtRest = true,
                KeyVaultKeyIdentifier = "https://vault.vault.azure.net/keys/data-protection",
                KeyVaultKeyRotationPeriod = TimeSpan.FromDays(30),
                KeyRingRetentionPeriod = TimeSpan.FromDays(365),
                KeyVaultKeyRetentionPeriod = TimeSpan.FromDays(730),
            };
        }
    }
}

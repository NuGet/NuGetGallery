// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Azure.Storage.Blobs;
using NuGetGallery.Authentication;

namespace NuGetGallery.DataProtection
{
    public class SharedDataProtectionConfiguration
    {
        public string StorageType { get; set; }
        public string StorageLocation { get; set; }
        public string ApplicationDiscriminator { get; set; } = SharedCookieConstants.DataProtectionApplicationName;
        public TimeSpan KeyLifetime { get; set; } = TimeSpan.FromDays(90);
        public bool EncryptKeysAtRest { get; set; }
        public string KeyVaultKeyIdentifier { get; set; }
        public TimeSpan KeyVaultKeyRotationPeriod { get; set; } = TimeSpan.FromDays(30);
        public TimeSpan KeyRingRetentionPeriod { get; set; } = TimeSpan.FromDays(365);
        public TimeSpan KeyVaultKeyRetentionPeriod { get; set; } = TimeSpan.FromDays(365);

        public void Validate(bool isProduction)
        {
            ValidateStorage();

            if (!string.Equals(
                ApplicationDiscriminator,
                SharedCookieConstants.DataProtectionApplicationName,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The Data Protection application discriminator is invalid.");
            }

            if (KeyLifetime < TimeSpan.FromDays(7))
            {
                throw new InvalidOperationException("The Data Protection key lifetime must be at least seven days.");
            }

            if (KeyRingRetentionPeriod < KeyLifetime)
            {
                throw new InvalidOperationException("The key-ring retention period must be at least the key lifetime.");
            }

            if (KeyVaultKeyRotationPeriod <= TimeSpan.Zero || KeyVaultKeyRotationPeriod >= KeyLifetime)
            {
                throw new InvalidOperationException("The Key Vault rotation period must be positive and shorter than the Data Protection key lifetime.");
            }

            if (KeyVaultKeyRetentionPeriod < KeyRingRetentionPeriod)
            {
                throw new InvalidOperationException("Key Vault key versions must be retained for at least the key-ring retention period.");
            }

            if (isProduction && !EncryptKeysAtRest)
            {
                throw new InvalidOperationException("Data Protection keys must be encrypted at rest in production.");
            }

            if (EncryptKeysAtRest)
            {
                ValidateVersionlessKeyIdentifier();
            }
            else if (!string.IsNullOrWhiteSpace(KeyVaultKeyIdentifier))
            {
                throw new InvalidOperationException("A Key Vault key identifier requires encryption at rest.");
            }
        }

        private void ValidateStorage()
        {
            if (!string.Equals(StorageType, DataProtectionStorageType.FileSystem, StringComparison.Ordinal)
                && !string.Equals(StorageType, DataProtectionStorageType.AzureStorage, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The Data Protection storage type is invalid.");
            }

            if (string.IsNullOrWhiteSpace(StorageLocation)
                || StorageLocation.IndexOf("$$secret$$", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException("The Data Protection storage location is missing or unresolved.");
            }

            try
            {
                if (string.Equals(StorageType, DataProtectionStorageType.AzureStorage, StringComparison.Ordinal))
                {
                    _ = new BlobServiceClient(StorageLocation);
                }
                else
                {
                    _ = Path.GetFullPath(StorageLocation);
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is FormatException
                || exception is NotSupportedException)
            {
                throw new InvalidOperationException("The Data Protection storage location is invalid.");
            }
        }

        private void ValidateVersionlessKeyIdentifier()
        {
            if (!Uri.TryCreate(KeyVaultKeyIdentifier, UriKind.Absolute, out var keyIdentifier)
                || !string.Equals(keyIdentifier.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrEmpty(keyIdentifier.UserInfo)
                || !keyIdentifier.IsDefaultPort
                || !string.IsNullOrEmpty(keyIdentifier.Query)
                || !string.IsNullOrEmpty(keyIdentifier.Fragment))
            {
                throw new InvalidOperationException("The Key Vault key identifier is invalid.");
            }

            var segments = keyIdentifier.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 2
                || !string.Equals(segments[0], "keys", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(segments[1]))
            {
                throw new InvalidOperationException("The Key Vault key identifier must be a versionless RSA or RSA-HSM key URI.");
            }
        }
    }
}

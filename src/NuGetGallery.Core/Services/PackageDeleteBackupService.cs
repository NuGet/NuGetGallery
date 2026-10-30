// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class PackageDeleteBackupService : IPackageDeleteBackupService
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Error,
            Converters = { new StringEnumConverter { AllowIntegerValues = false } },
        };

        private readonly ICoreFileStorageService _storage;

        public PackageDeleteBackupService(ICoreFileStorageService storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public async Task SaveAsync(PackageDeleteBackup backup)
        {
            if (backup == null)
            {
                throw new ArgumentNullException(nameof(backup));
            }

            var blobName = GetBlobName(backup.PackageId, backup.NormalizedVersion, backup.PackageDeleteKey, backup.PackageKey);
            ValidateBackup(backup);
            var json = JsonConvert.SerializeObject(backup, SerializerSettings);
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                await _storage.SaveFileAsync(CoreConstants.Folders.PackageBackupsFolderName,
                    blobName, CoreConstants.JsonContentType, stream);
            }
        }

        public async Task<PackageDeleteBackup> GetAsync(string packageId, string normalizedVersion, int packageDeleteKey, int packageKey)
        {
            var blobName = GetBlobName(packageId, normalizedVersion, packageDeleteKey, packageKey);
            using (var stream = await _storage.GetFileAsync(CoreConstants.Folders.PackageBackupsFolderName, blobName))
            {
                if (stream == null)
                {
                    return null;
                }

                using (var reader = new StreamReader(stream))
                {
                    var backup = JsonConvert.DeserializeObject<PackageDeleteBackup>(await reader.ReadToEndAsync(), SerializerSettings);
                    Require(backup != null, "Deletion backup must contain a JSON document.");
                    Require(backup.PackageDeleteKey == packageDeleteKey && backup.PackageKey == packageKey
                        && StringComparer.OrdinalIgnoreCase.Equals(backup.PackageId, packageId)
                        && StringComparer.OrdinalIgnoreCase.Equals(backup.NormalizedVersion, normalizedVersion),
                        "Deletion backup identity does not match the requested deletion and package.");
                    ValidateBackup(backup);
                    return backup;
                }
            }
        }

        public Task DeleteAsync(string packageId, string normalizedVersion, int packageDeleteKey, int packageKey)
        {
            return _storage.DeleteFileAsync(CoreConstants.Folders.PackageBackupsFolderName,
                GetBlobName(packageId, normalizedVersion, packageDeleteKey, packageKey));
        }

        private static void ValidateBackup(PackageDeleteBackup backup)
        {
            Require(backup.SchemaVersion == PackageDeleteBackup.CurrentSchemaVersion, "Unsupported deletion backup schema.");
            Require(!string.IsNullOrWhiteSpace(backup.PackageId)
                && NuGetVersion.TryParse(backup.NormalizedVersion, out var version)
                && string.Equals(version.ToNormalizedString(), backup.NormalizedVersion, StringComparison.Ordinal),
                "Deletion backup requires a package ID and normalized version.");
            Require(backup.Symbols != null && backup.IncomingDeprecationKeys != null,
                "Deletion backup collections must be present, even when empty.");
            ValidateBackupHashes(backup.PackageStatusKey, backup.PublicBackupHash, backup.ValidationBackupHash);

            var symbolKeys = new HashSet<int>();
            foreach (var symbol in backup.Symbols)
            {
                Require(symbol != null && symbol.SymbolPackageKey > 0 && symbolKeys.Add(symbol.SymbolPackageKey),
                    "Invalid or duplicate symbol key.");
                ValidateBackupHashes(symbol.SymbolPackageStatusKey, symbol.PublicBackupHash, symbol.ValidationBackupHash);
            }

            var deprecationKeys = new HashSet<int>();
            foreach (var deprecationKey in backup.IncomingDeprecationKeys)
            {
                Require(deprecationKey > 0 && deprecationKeys.Add(deprecationKey),
                    "Invalid or duplicate incoming deprecation key.");
            }
        }

        private static void ValidateBackupHashes(PackageStatus status, string publicBackupHash, string validationBackupHash)
        {
            Require(status == PackageStatus.Available || status == PackageStatus.Validating || status == PackageStatus.FailedValidation,
                "Unsupported pre-delete package or symbol status.");
            Require((publicBackupHash == null || IsSha512(publicBackupHash))
                && (validationBackupHash == null || IsSha512(validationBackupHash)),
                "Backup hashes must be SHA512 values or null for absent sources.");
            Require(status == PackageStatus.Available ? publicBackupHash != null : validationBackupHash != null,
                "Missing backup hash for the pre-delete package or symbol status.");
        }

        private static bool IsSha512(string hash) => Convert.FromBase64String(hash).Length == 64;

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidDataException(message);
            }
        }

        private static string GetBlobName(string packageId, string normalizedVersion, int packageDeleteKey, int packageKey)
        {
            if (string.IsNullOrWhiteSpace(packageId) || !PackageIdValidator.IsValidPackageIdForRead(packageId))
            {
                throw new ArgumentException("A valid package ID is required.", nameof(packageId));
            }

            if (!NuGetVersion.TryParse(normalizedVersion, out var version)
                || !string.Equals(version.ToNormalizedString(), normalizedVersion, StringComparison.Ordinal))
            {
                throw new ArgumentException("A normalized package version is required.", nameof(normalizedVersion));
            }

            if (packageDeleteKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(packageDeleteKey));
            }

            if (packageKey <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(packageKey));
            }

            // Share the archive's package/version prefix, but identify metadata by deletion rather than content hash.
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1}/package-delete-backup-{2}-{3}.json",
                packageId.ToLowerInvariant(), normalizedVersion.ToLowerInvariant(), packageDeleteKey, packageKey);
        }
    }
}
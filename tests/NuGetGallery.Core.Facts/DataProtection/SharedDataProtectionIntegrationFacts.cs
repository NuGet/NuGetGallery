// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NuGet.Services.KeyVault;
using NuGetGallery.Authentication;
using NuGetGallery.DataProtection;
using NuGetGallery.Diagnostics;
using Xunit;

namespace NuGetGallery
{
    public class SharedDataProtectionIntegrationFacts
    {
        private const string VersionlessKeyIdentifier = "https://vault.vault.azure.net/keys/data-protection";
        private const string Version1KeyIdentifier = VersionlessKeyIdentifier + "/version-1";
        private const string Version2KeyIdentifier = VersionlessKeyIdentifier + "/version-2";

        [Fact]
        public void OfficialKeyVaultIntegrationPreservesVersionedKeysAcrossRotationAndRestore()
        {
            var files = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
            FileStorageXmlRepository repository = CreateRepository(files);
            using (var resolver = new RotatingKeyResolver())
            {
                resolver.AddVersion(Version1KeyIdentifier, 0x31, makeCurrent: true);
                SharedDataProtectionConfiguration configuration = CreateConfiguration();

                string firstProtectedValue;
                using (ServiceProvider firstServices = BuildServices(repository, configuration, resolver))
                {
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    firstServices.GetRequiredService<IKeyManager>()
                        .CreateNewKey(now.AddDays(-2), now.AddDays(88));
                    firstProtectedValue = firstServices
                        .GetRequiredService<IDataProtectionProvider>()
                        .CreateProtector("integration")
                        .Protect("first-value");
                }

                resolver.AddVersion(Version2KeyIdentifier, 0x62, makeCurrent: true);
                using (ServiceProvider rotationServices = BuildServices(repository, configuration, resolver))
                {
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    rotationServices.GetRequiredService<IKeyManager>()
                        .CreateNewKey(now.AddDays(-1), now.AddDays(89));
                }

                string allXml = string.Join(
                    Environment.NewLine,
                    files.Values.Select(bytes => System.Text.Encoding.UTF8.GetString(bytes)));
                Assert.Contains(Version1KeyIdentifier, allXml);
                Assert.Contains(Version2KeyIdentifier, allXml);
                Assert.DoesNotContain("<masterKey", allXml);
                Assert.Equal(2, files.Count);

                string secondProtectedValue;
                using (ServiceProvider restoredServices = BuildServices(repository, CreateConfiguration(), resolver))
                {
                    IDataProtector restoredProtector = restoredServices
                        .GetRequiredService<IDataProtectionProvider>()
                        .CreateProtector("integration");
                    Assert.Equal("first-value", restoredProtector.Unprotect(firstProtectedValue));
                    secondProtectedValue = restoredProtector.Protect("second-value");
                    Assert.Equal("second-value", restoredProtector.Unprotect(secondProtectedValue));
                }

                resolver.RemoveVersion(Version1KeyIdentifier);
                using (ServiceProvider missingOldVersionServices = BuildServices(repository, CreateConfiguration(), resolver))
                {
                    IDataProtector protector = missingOldVersionServices
                        .GetRequiredService<IDataProtectionProvider>()
                        .CreateProtector("integration");
                    Assert.ThrowsAny<Exception>(() => protector.Unprotect(firstProtectedValue));
                    Assert.Equal("second-value", protector.Unprotect(secondProtectedValue));
                }

                Assert.Equal(2, files.Count);
                Assert.True(resolver.ValidationCount >= 4);
            }
        }

        [Fact]
        public void ProductionRegistrationRejectsResolverWithoutMetadataValidation()
        {
            var services = new ServiceCollection();
            FileStorageXmlRepository repository = CreateRepository(
                new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => services
                    .AddDataProtection()
                    .ConfigureSharedDataProtection(
                        repository,
                        CreateConfiguration(),
                        Mock.Of<IKeyEncryptionKeyResolver>(),
                        isProduction: true));

            Assert.Contains("validates key metadata", exception.Message);
        }

        private static ServiceProvider BuildServices(
            FileStorageXmlRepository repository,
            SharedDataProtectionConfiguration configuration,
            IKeyEncryptionKeyResolver resolver)
        {
            var services = new ServiceCollection();
            services
                .AddDataProtection()
                .ConfigureSharedDataProtection(
                    repository,
                    configuration,
                    resolver,
                    isProduction: true);
            return services.BuildServiceProvider();
        }

        private static FileStorageXmlRepository CreateRepository(
            ConcurrentDictionary<string, byte[]> files)
        {
            var storage = new Mock<ICoreFileStorageService>();
            storage
                .Setup(x => x.ListFilesAsync(CoreConstants.Folders.DataProtectionFolderName))
                .ReturnsAsync(() => (IReadOnlyList<string>)files.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList());
            storage
                .Setup(x => x.GetFileAsync(CoreConstants.Folders.DataProtectionFolderName, It.IsAny<string>()))
                .ReturnsAsync((string _, string fileName) =>
                    files.TryGetValue(fileName, out byte[] content)
                        ? new MemoryStream(content, writable: false)
                        : null);
            storage
                .Setup(x => x.SaveFileAsync(
                    CoreConstants.Folders.DataProtectionFolderName,
                    It.IsAny<string>(),
                    CoreConstants.XmlContentType,
                    It.IsAny<Stream>(),
                    false))
                .Returns<string, string, string, Stream, bool>((_, fileName, __, stream, ___) =>
                {
                    using (var copy = new MemoryStream())
                    {
                        stream.CopyTo(copy);
                        if (!files.TryAdd(fileName, copy.ToArray()))
                        {
                            throw new FileAlreadyExistsException("The immutable key already exists.");
                        }
                    }

                    return Task.CompletedTask;
                });

            return new FileStorageXmlRepository(storage.Object, Mock.Of<IDiagnosticsService>());
        }

        private static SharedDataProtectionConfiguration CreateConfiguration()
        {
            return new SharedDataProtectionConfiguration
            {
                StorageType = DataProtectionStorageType.AzureStorage,
                StorageLocation = "UseDevelopmentStorage=true",
                ApplicationDiscriminator = SharedCookieConstants.DataProtectionApplicationName,
                KeyLifetime = TimeSpan.FromDays(90),
                EncryptKeysAtRest = true,
                KeyVaultKeyIdentifier = VersionlessKeyIdentifier,
                KeyVaultKeyRotationPeriod = TimeSpan.FromDays(30),
                KeyRingRetentionPeriod = TimeSpan.FromDays(365),
                KeyVaultKeyRetentionPeriod = TimeSpan.FromDays(730),
            };
        }

        private sealed class RotatingKeyResolver :
            IKeyEncryptionKeyResolver,
            IKeyEncryptionKeyMetadataValidator,
            IDisposable
        {
            private readonly Dictionary<string, TestKeyEncryptionKey> _keys =
                new Dictionary<string, TestKeyEncryptionKey>(StringComparer.OrdinalIgnoreCase);
            private string _currentKeyIdentifier;

            public int ValidationCount { get; private set; }

            public void AddVersion(string keyIdentifier, byte mask, bool makeCurrent)
            {
                _keys.Add(keyIdentifier, new TestKeyEncryptionKey(keyIdentifier, mask));
                if (makeCurrent)
                {
                    _currentKeyIdentifier = keyIdentifier;
                }
            }

            public void RemoveVersion(string keyIdentifier)
            {
                _keys.Remove(keyIdentifier);
            }

            public IKeyEncryptionKey Resolve(
                string keyId,
                CancellationToken cancellationToken = default)
            {
                string resolvedIdentifier = string.Equals(
                    keyId.TrimEnd('/'),
                    VersionlessKeyIdentifier,
                    StringComparison.OrdinalIgnoreCase)
                    ? _currentKeyIdentifier
                    : keyId;

                if (!_keys.TryGetValue(resolvedIdentifier, out TestKeyEncryptionKey key))
                {
                    throw new CryptographicException("The referenced Key Vault key version is unavailable.");
                }

                return key;
            }

            public Task<IKeyEncryptionKey> ResolveAsync(
                string keyId,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Resolve(keyId, cancellationToken));
            }

            public void ValidateKey(
                string keyId,
                CancellationToken cancellationToken = default)
            {
                if (!string.Equals(
                    keyId.TrimEnd('/'),
                    VersionlessKeyIdentifier,
                    StringComparison.OrdinalIgnoreCase)
                    || !_keys.ContainsKey(_currentKeyIdentifier))
                {
                    throw new InvalidOperationException("The key metadata is invalid.");
                }

                ValidationCount++;
            }

            public void Dispose()
            {
            }
        }

        private sealed class TestKeyEncryptionKey : IKeyEncryptionKey
        {
            private readonly byte _mask;

            public TestKeyEncryptionKey(string keyId, byte mask)
            {
                KeyId = keyId;
                _mask = mask;
            }

            public string KeyId { get; }

            public byte[] WrapKey(
                string algorithm,
                ReadOnlyMemory<byte> key,
                CancellationToken cancellationToken = default)
            {
                return Transform(key);
            }

            public Task<byte[]> WrapKeyAsync(
                string algorithm,
                ReadOnlyMemory<byte> key,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Transform(key));
            }

            public byte[] UnwrapKey(
                string algorithm,
                ReadOnlyMemory<byte> encryptedKey,
                CancellationToken cancellationToken = default)
            {
                return Transform(encryptedKey);
            }

            public Task<byte[]> UnwrapKeyAsync(
                string algorithm,
                ReadOnlyMemory<byte> encryptedKey,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Transform(encryptedKey));
            }

            private byte[] Transform(ReadOnlyMemory<byte> value)
            {
                byte[] result = value.ToArray();
                for (int index = 0; index < result.Length; index++)
                {
                    result[index] ^= _mask;
                }

                return result;
            }
        }
    }
}

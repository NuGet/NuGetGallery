// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core.Cryptography;
using Azure.Security.KeyVault.Keys;
using Xunit;

namespace NuGet.Services.KeyVault.Tests
{
    public class KeyVaultKeyEncryptionKeyResolverFacts
    {
        private static readonly Uri VaultUri = new Uri("https://test-vault.vault.azure.net/");
        private static readonly Uri VersionedKeyUri = new Uri("https://test-vault.vault.azure.net/keys/test-key/version-1");
        private static readonly Uri VersionedKeyUri2 = new Uri("https://test-vault.vault.azure.net/keys/test-key/version-2");

        [Fact]
        public void ImplementsOfficialResolverContract()
        {
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver();

            Assert.IsAssignableFrom<IKeyEncryptionKeyResolver>(target);
            Assert.IsAssignableFrom<IKeyEncryptionKeyMetadataValidator>(target);
        }

        [Fact]
        public void ValidateKeyAcceptsEnabledRsaAndRsaHsmKeys()
        {
            foreach (KeyType keyType in new[] { KeyType.Rsa, KeyType.RsaHsm })
            {
                var cancellationTokenSource = new CancellationTokenSource();
                KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                    getKey: (name, cancellationToken) =>
                    {
                        Assert.Equal("test-key", name);
                        Assert.Equal(cancellationTokenSource.Token, cancellationToken);
                        return CreateKey(VersionedKeyUri, keyType);
                    });

                target.ValidateKey(
                    "https://test-vault.vault.azure.net/keys/test-key",
                    cancellationTokenSource.Token);
            }
        }

        [Fact]
        public void ValidateKeyRejectsVersionedOrForeignIdentifiersBeforeMetadataAccess()
        {
            int metadataAccessCount = 0;
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                getKey: (_, __) =>
                {
                    metadataAccessCount++;
                    return CreateKey(VersionedKeyUri, KeyType.Rsa);
                });

            Assert.Throws<ArgumentException>(() => target.ValidateKey(VersionedKeyUri.AbsoluteUri));
            Assert.Throws<ArgumentException>(() => target.ValidateKey(
                "https://other-vault.vault.azure.net/keys/test-key"));
            Assert.Equal(0, metadataAccessCount);
        }

        [Fact]
        public void ValidateKeyRejectsUnexpectedReturnedIdentity()
        {
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                getKey: (_, __) => CreateKey(
                    new Uri("https://other-vault.vault.azure.net/keys/test-key/version-1"),
                    KeyType.Rsa));

            Assert.Throws<ArgumentException>(() => target.ValidateKey(
                "https://test-vault.vault.azure.net/keys/test-key"));
        }

        [Fact]
        public void ValidateKeyRejectsDifferentReturnedKeyName()
        {
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                getKey: (_, __) => CreateKey(
                    new Uri("https://test-vault.vault.azure.net/keys/other-key/version-1"),
                    KeyType.Rsa));

            Assert.Throws<InvalidOperationException>(() => target.ValidateKey(
                "https://test-vault.vault.azure.net/keys/test-key"));
        }

        [Fact]
        public void ValidateKeyRejectsDisabledOrUnavailableKeys()
        {
            foreach (KeyVaultKey key in new[]
            {
                CreateKey(VersionedKeyUri, KeyType.Rsa, enabled: null),
                CreateKey(VersionedKeyUri, KeyType.Rsa, enabled: false),
                CreateKey(VersionedKeyUri, KeyType.Rsa, notBefore: DateTimeOffset.UtcNow.AddDays(1)),
                CreateKey(VersionedKeyUri, KeyType.Rsa, expiresOn: DateTimeOffset.UtcNow.AddDays(-1)),
            })
            {
                KeyVaultKeyEncryptionKeyResolver target = CreateResolver(getKey: (_, __) => key);

                Assert.Throws<InvalidOperationException>(() => target.ValidateKey(
                    "https://test-vault.vault.azure.net/keys/test-key"));
            }
        }

        [Fact]
        public void ValidateKeyRejectsUnsupportedTypeOrOperations()
        {
            foreach (KeyVaultKey key in new[]
            {
                CreateKey(VersionedKeyUri, KeyType.Ec),
                CreateKey(
                    VersionedKeyUri,
                    KeyType.Rsa,
                    operations: new[] { KeyOperation.Encrypt, KeyOperation.Decrypt }),
            })
            {
                KeyVaultKeyEncryptionKeyResolver target = CreateResolver(getKey: (_, __) => key);

                Assert.Throws<InvalidOperationException>(() => target.ValidateKey(
                    "https://test-vault.vault.azure.net/keys/test-key"));
            }
        }

        [Fact]
        public void ValidateKeyPreservesAzureSdkFailure()
        {
            var expected = new RequestFailedException(403, "Forbidden");
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                getKey: (_, __) => throw expected);

            RequestFailedException actual = Assert.Throws<RequestFailedException>(
                () => target.ValidateKey("https://test-vault.vault.azure.net/keys/test-key"));

            Assert.Same(expected, actual);
        }

        [Fact]
        public void VersionedIdentifiersAreCachedWithoutResolvingLatestVersion()
        {
            int syncResolutionCount = 0;
            int clientCount = 0;
            var expected = new TestKeyEncryptionKey(VersionedKeyUri.AbsoluteUri);
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                resolveVersionedKeyIdentifier: (_, __) =>
                {
                    syncResolutionCount++;
                    return VersionedKeyUri;
                },
                createKeyEncryptionKey: _ =>
                {
                    clientCount++;
                    return expected;
                });

            IKeyEncryptionKey first = target.Resolve(VersionedKeyUri.AbsoluteUri);
            IKeyEncryptionKey second = target.Resolve(VersionedKeyUri.AbsoluteUri);

            Assert.Same(expected, first);
            Assert.Same(first, second);
            Assert.Equal(0, syncResolutionCount);
            Assert.Equal(1, clientCount);
        }

        [Fact]
        public void VersionlessIdentifiersAreResolvedAfreshAndCacheVersionedClients()
        {
            int resolutionCount = 0;
            int clientCount = 0;
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                resolveVersionedKeyIdentifier: (_, __) =>
                {
                    resolutionCount++;
                    return VersionedKeyUri;
                },
                createKeyEncryptionKey: id =>
                {
                    clientCount++;
                    return new TestKeyEncryptionKey(id.AbsoluteUri);
                });

            IKeyEncryptionKey first = target.Resolve("https://test-vault.vault.azure.net/keys/test-key");
            IKeyEncryptionKey second = target.Resolve("https://test-vault.vault.azure.net/keys/test-key");

            Assert.Same(first, second);
            Assert.Equal(2, resolutionCount);
            Assert.Equal(1, clientCount);
        }

        [Fact]
        public void VersionlessIdentifiersObserveKeyRotation()
        {
            var versions = new Queue<Uri>(new[] { VersionedKeyUri, VersionedKeyUri2 });
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                resolveVersionedKeyIdentifier: (_, __) => versions.Dequeue(),
                createKeyEncryptionKey: id => new TestKeyEncryptionKey(id.AbsoluteUri));

            IKeyEncryptionKey first = target.Resolve("https://test-vault.vault.azure.net/keys/test-key");
            IKeyEncryptionKey second = target.Resolve("https://test-vault.vault.azure.net/keys/test-key");

            Assert.Equal(VersionedKeyUri.AbsoluteUri, first.KeyId);
            Assert.Equal(VersionedKeyUri2.AbsoluteUri, second.KeyId);
            Assert.NotSame(first, second);
        }

        [Fact]
        public async Task ResolveAsyncUsesNativeAsyncResolutionAndCachesVersionedClient()
        {
            int syncResolutionCount = 0;
            int asyncResolutionCount = 0;
            int clientCount = 0;
            var cancellationTokenSource = new CancellationTokenSource();
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                resolveVersionedKeyIdentifier: (_, __) =>
                {
                    syncResolutionCount++;
                    return VersionedKeyUri;
                },
                resolveVersionedKeyIdentifierAsync: (_, cancellationToken) =>
                {
                    Assert.Equal(cancellationTokenSource.Token, cancellationToken);
                    asyncResolutionCount++;
                    return Task.FromResult(VersionedKeyUri);
                },
                createKeyEncryptionKey: id =>
                {
                    clientCount++;
                    return new TestKeyEncryptionKey(id.AbsoluteUri);
                });

            IKeyEncryptionKey first = await target.ResolveAsync(
                "https://test-vault.vault.azure.net/keys/test-key",
                cancellationTokenSource.Token);
            IKeyEncryptionKey second = await target.ResolveAsync(
                "https://test-vault.vault.azure.net/keys/test-key",
                cancellationTokenSource.Token);

            Assert.Same(first, second);
            Assert.Equal(0, syncResolutionCount);
            Assert.Equal(2, asyncResolutionCount);
            Assert.Equal(1, clientCount);
        }

        [Theory]
        [InlineData("http://test-vault.vault.azure.net/keys/test-key/version-1")]
        [InlineData("https://other-vault.vault.azure.net/keys/test-key/version-1")]
        [InlineData("https://user@test-vault.vault.azure.net/keys/test-key/version-1")]
        [InlineData("https://test-vault.vault.azure.net:444/keys/test-key/version-1")]
        [InlineData("https://test-vault.vault.azure.net/secrets/test-key/version-1")]
        [InlineData("https://test-vault.vault.azure.net/keys/test-key/version-1/extra")]
        [InlineData("https://test-vault.vault.azure.net/keys/test-key/version-1?query=value")]
        [InlineData("https://test-vault.vault.azure.net/keys/test-key/version-1#fragment")]
        public void RejectsUnexpectedUrisBeforeClientAccess(string keyId)
        {
            int accessCount = 0;
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                resolveVersionedKeyIdentifier: (_, __) =>
                {
                    accessCount++;
                    return VersionedKeyUri;
                },
                createKeyEncryptionKey: id =>
                {
                    accessCount++;
                    return new TestKeyEncryptionKey(id.AbsoluteUri);
                });

            Assert.Throws<ArgumentException>(() => target.Resolve(keyId));
            Assert.Equal(0, accessCount);
        }

        [Fact]
        public void RejectsNullIdentifierBeforeClientAccess()
        {
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver();

            Assert.Throws<ArgumentNullException>(() => target.Resolve(null));
        }

        [Fact]
        public void RejectsUnexpectedIdentifierReturnedByVault()
        {
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                resolveVersionedKeyIdentifier: (_, __) =>
                    new Uri("https://other-vault.vault.azure.net/keys/test-key/version-1"));

            Assert.Throws<ArgumentException>(
                () => target.Resolve("https://test-vault.vault.azure.net/keys/test-key"));
        }

        [Fact]
        public void RejectsDifferentKeyNameReturnedByVault()
        {
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                resolveVersionedKeyIdentifier: (_, __) =>
                    new Uri("https://test-vault.vault.azure.net/keys/other-key/version-1"));

            Assert.Throws<InvalidOperationException>(
                () => target.Resolve("https://test-vault.vault.azure.net/keys/test-key"));
        }

        [Fact]
        public void PreservesAzureSdkFailure()
        {
            var expected = new RequestFailedException(403, "Forbidden");
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                resolveVersionedKeyIdentifier: (_, __) => throw expected);

            RequestFailedException actual = Assert.Throws<RequestFailedException>(
                () => target.Resolve("https://test-vault.vault.azure.net/keys/test-key"));

            Assert.Same(expected, actual);
        }

        [Fact]
        public async Task PreservesAzureSdkFailureAsync()
        {
            var expected = new RequestFailedException(403, "Forbidden");
            KeyVaultKeyEncryptionKeyResolver target = CreateResolver(
                resolveVersionedKeyIdentifierAsync: (_, __) => Task.FromException<Uri>(expected));

            RequestFailedException actual = await Assert.ThrowsAsync<RequestFailedException>(
                () => target.ResolveAsync("https://test-vault.vault.azure.net/keys/test-key"));

            Assert.Same(expected, actual);
        }

        private static KeyVaultKeyEncryptionKeyResolver CreateResolver(
            Func<string, CancellationToken, Uri> resolveVersionedKeyIdentifier = null,
            Func<string, CancellationToken, Task<Uri>> resolveVersionedKeyIdentifierAsync = null,
            Func<Uri, IKeyEncryptionKey> createKeyEncryptionKey = null,
            Func<string, CancellationToken, KeyVaultKey> getKey = null)
        {
            return new KeyVaultKeyEncryptionKeyResolver(
                VaultUri,
                resolveVersionedKeyIdentifier ?? ((_, __) => VersionedKeyUri),
                resolveVersionedKeyIdentifierAsync ?? ((_, __) => Task.FromResult(VersionedKeyUri)),
                createKeyEncryptionKey ?? (id => new TestKeyEncryptionKey(id.AbsoluteUri)),
                getKey);
        }

        private static KeyVaultKey CreateKey(
            Uri identifier,
            KeyType keyType,
            bool? enabled = true,
            DateTimeOffset? notBefore = null,
            DateTimeOffset? expiresOn = null,
            IEnumerable<KeyOperation> operations = null)
        {
            KeyProperties properties = KeyModelFactory.KeyProperties(id: identifier);
            properties.Enabled = enabled;
            properties.NotBefore = notBefore;
            properties.ExpiresOn = expiresOn;
            return KeyModelFactory.KeyVaultKey(
                properties,
                KeyModelFactory.JsonWebKey(
                    keyType,
                    id: identifier.AbsoluteUri,
                    keyOps: operations ?? new[] { KeyOperation.WrapKey, KeyOperation.UnwrapKey }));
        }

        private sealed class TestKeyEncryptionKey : IKeyEncryptionKey
        {
            public TestKeyEncryptionKey(string keyId)
            {
                KeyId = keyId;
            }

            public string KeyId { get; }

            public byte[] WrapKey(
                string algorithm,
                ReadOnlyMemory<byte> key,
                CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<byte[]> WrapKeyAsync(
                string algorithm,
                ReadOnlyMemory<byte> key,
                CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public byte[] UnwrapKey(
                string algorithm,
                ReadOnlyMemory<byte> encryptedKey,
                CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<byte[]> UnwrapKeyAsync(
                string algorithm,
                ReadOnlyMemory<byte> encryptedKey,
                CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Cryptography;
using Azure.Security.KeyVault.Keys;

namespace NuGet.Services.KeyVault
{
    public class KeyVaultKeyEncryptionKeyResolver : IKeyEncryptionKeyResolver, IKeyEncryptionKeyMetadataValidator
    {
        private readonly Uri _vaultUri;
        private readonly Func<string, CancellationToken, Uri> _resolveVersionedKeyIdentifier;
        private readonly Func<string, CancellationToken, Task<Uri>> _resolveVersionedKeyIdentifierAsync;
        private readonly Func<Uri, IKeyEncryptionKey> _createKeyEncryptionKey;
        private readonly Func<string, CancellationToken, KeyVaultKey> _getKey;
        private readonly ConcurrentDictionary<string, IKeyEncryptionKey> _versionedClients;

        public KeyVaultKeyEncryptionKeyResolver(KeyVaultConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var clientFactory = new KeyVaultClientFactory(configuration);
            KeyClient keyClient = clientFactory.CreateKeyClient();

            _vaultUri = clientFactory.VaultUri;
            _resolveVersionedKeyIdentifier = (name, cancellationToken) => ValidateKeyMetadata(
                name,
                keyClient.GetKey(name, cancellationToken: cancellationToken).Value);
            _resolveVersionedKeyIdentifierAsync = async (name, cancellationToken) => ValidateKeyMetadata(
                name,
                (await keyClient.GetKeyAsync(name, cancellationToken: cancellationToken).ConfigureAwait(false)).Value);
            _createKeyEncryptionKey = keyIdentifier => clientFactory.CreateCryptographyClient(keyIdentifier);
            _getKey = (name, cancellationToken) =>
                keyClient.GetKey(name, cancellationToken: cancellationToken).Value;
            _versionedClients = new ConcurrentDictionary<string, IKeyEncryptionKey>(StringComparer.OrdinalIgnoreCase);
        }

        internal KeyVaultKeyEncryptionKeyResolver(
            Uri vaultUri,
            Func<string, CancellationToken, Uri> resolveVersionedKeyIdentifier,
            Func<string, CancellationToken, Task<Uri>> resolveVersionedKeyIdentifierAsync,
            Func<Uri, IKeyEncryptionKey> createKeyEncryptionKey,
            Func<string, CancellationToken, KeyVaultKey> getKey = null)
        {
            _vaultUri = vaultUri ?? throw new ArgumentNullException(nameof(vaultUri));
            _resolveVersionedKeyIdentifier = resolveVersionedKeyIdentifier ?? throw new ArgumentNullException(nameof(resolveVersionedKeyIdentifier));
            _resolveVersionedKeyIdentifierAsync = resolveVersionedKeyIdentifierAsync ?? throw new ArgumentNullException(nameof(resolveVersionedKeyIdentifierAsync));
            _createKeyEncryptionKey = createKeyEncryptionKey ?? throw new ArgumentNullException(nameof(createKeyEncryptionKey));
            _getKey = getKey;
            _versionedClients = new ConcurrentDictionary<string, IKeyEncryptionKey>(StringComparer.OrdinalIgnoreCase);
        }

        public void ValidateKey(string keyId, CancellationToken cancellationToken = default)
        {
            KeyVaultKeyIdentifier requestedIdentifier = KeyVaultUriValidator.ValidateKeyIdentifier(keyId, _vaultUri);
            if (!string.IsNullOrEmpty(requestedIdentifier.Version))
            {
                throw new ArgumentException("The Data Protection Key Vault key identifier must be versionless.", nameof(keyId));
            }

            if (_getKey == null)
            {
                throw new InvalidOperationException("Key metadata validation is unavailable.");
            }

            ValidateKeyMetadata(
                requestedIdentifier.Name,
                _getKey(requestedIdentifier.Name, cancellationToken));
        }

        private Uri ValidateKeyMetadata(string requestedName, KeyVaultKey key)
        {
            if (key == null || key.Id == null)
            {
                throw new InvalidOperationException("Key Vault returned incomplete key metadata.");
            }

            KeyVaultKeyIdentifier returnedIdentifier = KeyVaultUriValidator.ValidateKeyIdentifier(key.Id, _vaultUri);
            if (string.IsNullOrEmpty(returnedIdentifier.Version)
                || !string.Equals(requestedName, returnedIdentifier.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Key Vault returned an invalid versioned key identifier.");
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (key.Properties.Enabled != true
                || (key.Properties.NotBefore.HasValue && key.Properties.NotBefore.Value > now)
                || (key.Properties.ExpiresOn.HasValue && key.Properties.ExpiresOn.Value <= now))
            {
                throw new InvalidOperationException("The Data Protection Key Vault key is not enabled and currently usable.");
            }

            if (key.Key == null
                || (key.KeyType != KeyType.Rsa && key.KeyType != KeyType.RsaHsm)
                || !key.KeyOperations.Contains(KeyOperation.WrapKey)
                || !key.KeyOperations.Contains(KeyOperation.UnwrapKey))
            {
                throw new InvalidOperationException("The Data Protection Key Vault key must be an RSA or RSA-HSM key with wrapKey and unwrapKey operations.");
            }

            return returnedIdentifier.SourceId;
        }

        public IKeyEncryptionKey Resolve(string keyId, CancellationToken cancellationToken = default)
        {
            KeyVaultKeyIdentifier identifier = KeyVaultUriValidator.ValidateKeyIdentifier(keyId, _vaultUri);
            if (!string.IsNullOrEmpty(identifier.Version))
            {
                return GetOrCreateVersionedClient(identifier);
            }

            Uri versionedKeyId = _resolveVersionedKeyIdentifier(identifier.Name, cancellationToken);
            KeyVaultKeyIdentifier versionedIdentifier = ValidateResolvedIdentifier(identifier, versionedKeyId);
            return GetOrCreateVersionedClient(versionedIdentifier);
        }

        public async Task<IKeyEncryptionKey> ResolveAsync(string keyId, CancellationToken cancellationToken = default)
        {
            KeyVaultKeyIdentifier identifier = KeyVaultUriValidator.ValidateKeyIdentifier(keyId, _vaultUri);
            if (!string.IsNullOrEmpty(identifier.Version))
            {
                return GetOrCreateVersionedClient(identifier);
            }

            Uri versionedKeyId = await _resolveVersionedKeyIdentifierAsync(identifier.Name, cancellationToken).ConfigureAwait(false);
            KeyVaultKeyIdentifier versionedIdentifier = ValidateResolvedIdentifier(identifier, versionedKeyId);
            return GetOrCreateVersionedClient(versionedIdentifier);
        }

        private IKeyEncryptionKey GetOrCreateVersionedClient(KeyVaultKeyIdentifier identifier)
        {
            return _versionedClients.GetOrAdd(
                identifier.SourceId.AbsoluteUri,
                _ => _createKeyEncryptionKey(identifier.SourceId));
        }

        private KeyVaultKeyIdentifier ValidateResolvedIdentifier(
            KeyVaultKeyIdentifier requestedIdentifier,
            Uri versionedKeyId)
        {
            KeyVaultKeyIdentifier versionedIdentifier = KeyVaultUriValidator.ValidateKeyIdentifier(versionedKeyId, _vaultUri);
            if (string.IsNullOrEmpty(versionedIdentifier.Version)
                || !string.Equals(requestedIdentifier.Name, versionedIdentifier.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Key Vault returned an invalid versioned key identifier.");
            }

            return versionedIdentifier;
        }
    }
}

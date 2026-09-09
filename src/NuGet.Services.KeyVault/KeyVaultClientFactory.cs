// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure.Core;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Secrets;

namespace NuGet.Services.KeyVault
{
    public class KeyVaultClientFactory
    {
        private readonly TokenCredential _credential;

        public KeyVaultClientFactory(KeyVaultConfiguration configuration)
            : this(configuration, new KeyVaultCredentialFactory())
        {
        }

        internal KeyVaultClientFactory(
            KeyVaultConfiguration configuration,
            KeyVaultCredentialFactory credentialFactory)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (credentialFactory == null)
            {
                throw new ArgumentNullException(nameof(credentialFactory));
            }

            VaultUri = KeyVaultUriValidator.CreateVaultUri(configuration.VaultName);
            _credential = credentialFactory.CreateCredential(configuration);
        }

        public Uri VaultUri { get; }

        public virtual SecretClient CreateSecretClient()
        {
            return new SecretClient(VaultUri, _credential);
        }

        public virtual KeyClient CreateKeyClient()
        {
            return new KeyClient(VaultUri, _credential);
        }

        public virtual CryptographyClient CreateCryptographyClient(Uri keyIdentifier)
        {
            KeyVaultKeyIdentifier identifier = KeyVaultUriValidator.ValidateKeyIdentifier(keyIdentifier, VaultUri);
            return new CryptographyClient(identifier.SourceId, _credential);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace NuGet.Services.KeyVault
{
    public class KeyVaultDataSigner : IKeyVaultDataSigner
    {
        private readonly CryptographyClient _cryptoClient;

        public KeyVaultDataSigner(CryptographyClient cryptoClient)
        {
            _cryptoClient = cryptoClient ?? throw new ArgumentNullException(nameof(cryptoClient));
        }

        public async Task<byte[]> SignDataAsync(byte[] digest, KeyVaultSignatureAlgorithm algorithm)
        {
            if (digest is null)
            {
                throw new ArgumentNullException(nameof(digest));
            }

            SignatureAlgorithm libraryAlgorithm = algorithm switch
            {
                KeyVaultSignatureAlgorithm.RS256 => SignatureAlgorithm.RS256,
                KeyVaultSignatureAlgorithm.RS384 => SignatureAlgorithm.RS384,
                KeyVaultSignatureAlgorithm.RS512 => SignatureAlgorithm.RS512,
                KeyVaultSignatureAlgorithm.ES256 => SignatureAlgorithm.ES256,
                KeyVaultSignatureAlgorithm.ES384 => SignatureAlgorithm.ES384,
                KeyVaultSignatureAlgorithm.ES512 => SignatureAlgorithm.ES512,
                _ => throw new ArgumentException($"Unsupported algorithm: {algorithm}", nameof(algorithm)),
            };

            SignResult result = await _cryptoClient.SignDataAsync(libraryAlgorithm, digest);
            return result.Signature;
        }
    }
}

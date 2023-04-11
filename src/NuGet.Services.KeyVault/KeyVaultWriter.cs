// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;

namespace NuGet.Services.KeyVault
{
    public class KeyVaultWriter : KeyVaultReader, ISecretWriter
    {
        public KeyVaultWriter(KeyVaultConfiguration configuration) : base(configuration)
        {
        }

        public async Task SetSecretAsync(
            string secretName,
            string secretValue,
            DateTimeOffset? expiration = null)
        {
            SecretAttributes attributes = null;
            if (expiration.HasValue)
            {
                attributes = new SecretAttributes
                {
                    Expires = expiration.Value.UtcDateTime,
                };
            }

            await KeyVaultClient.SetSecretAsync(VaultBaseUrl, secretName, secretValue, secretAttributes: attributes);
        }
    }
}

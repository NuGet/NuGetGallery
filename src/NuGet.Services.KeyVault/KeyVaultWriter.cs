// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

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
            var secret = new Azure.Security.KeyVault.Secrets.KeyVaultSecret(secretName, secretValue);
            secret.Properties.ExpiresOn = expiration;

            await KeyVaultClient.SetSecretAsync(secret);
        }
    }
}

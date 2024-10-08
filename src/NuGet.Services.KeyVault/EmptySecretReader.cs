// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.KeyVault
{
    public class EmptySecretReader : ICachingSecretReader
    {
        public string GetSecret(string secretName)
        {
            return GetSecret(secretName, logger: null);
        }

        public string GetSecret(string secretName, ILogger logger)
        {
            return secretName;
        }

        public Task<string> GetSecretAsync(string secretName) => GetSecretAsync(secretName, logger: null);

        public Task<string> GetSecretAsync(string secretName, ILogger logger)
        {
            return Task.FromResult(secretName);
        }

        public ISecret GetSecretObject(string secretName)
        {
            return GetSecretObject(secretName, logger: null);
        }

        public ISecret GetSecretObject(string secretName, ILogger logger)
        {
            return new KeyVaultSecret(secretName, secretName, null);
        }

        public Task<ISecret> GetSecretObjectAsync(string secretName) => GetSecretObjectAsync(secretName, logger: null);

        public Task<ISecret> GetSecretObjectAsync(string secretName, ILogger logger)
        {
            return Task.FromResult(GetSecretObject(secretName, logger));
        }

        public bool TryGetCachedSecret(string secretName, out string secretValue) => TryGetCachedSecret(secretName, logger: null, out secretValue);

        public bool TryGetCachedSecret(string secretName, ILogger logger, out string secretValue)
        {
            secretValue = secretName;
            return true;
        }

        public bool TryGetCachedSecretObject(string secretName, out ISecret secretObject) => TryGetCachedSecretObject(secretName, logger: null, out secretObject);

        public bool TryGetCachedSecretObject(string secretName, ILogger logger, out ISecret secretObject)
        {
            secretObject = new KeyVaultSecret(secretName, secretName, null);
            return true;
        }
    }
}

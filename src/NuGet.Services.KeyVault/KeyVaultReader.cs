// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using AzureSecurityKeyVaultSecret = Azure.Security.KeyVault.Secrets.KeyVaultSecret;

namespace NuGet.Services.KeyVault
{
    /// <summary>
    /// Reads secretes from KeyVault.
    /// Authentication with KeyVault is done using either a managed identity or a certificate in location:LocalMachine store name:My
    /// </summary>
    public class KeyVaultReader : ISecretReader
    {
        private readonly KeyVaultConfiguration _configuration;
        private readonly Lazy<SecretClient> _keyVaultClient;
        protected SecretClient KeyVaultClient => _keyVaultClient.Value;
        internal bool _testMode;
        internal bool _isUsingSendx5c;

        internal KeyVaultReader(SecretClient secretClient, KeyVaultConfiguration configuration, bool testMode = false)
        {
            _configuration = configuration;
            _keyVaultClient = new Lazy<SecretClient>(() => secretClient);
            _testMode = testMode;
            InitializeClient();
        }

        public KeyVaultReader(KeyVaultConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _configuration = configuration;
            _keyVaultClient = new Lazy<SecretClient>(InitializeClient);
        }

        public string GetSecret(string secretName)
        {
            return GetSecret(secretName, logger: null);
        }

        public string GetSecret(string secretName, ILogger logger)
        {
            AzureSecurityKeyVaultSecret secret = _keyVaultClient.Value.GetSecret(secretName);
            return secret.Value;
        }

        public async Task<string> GetSecretAsync(string secretName)
        {
            return await GetSecretAsync(secretName, logger: null);
        }

        public async Task<string> GetSecretAsync(string secretName, ILogger logger)
        {
            AzureSecurityKeyVaultSecret secret = await _keyVaultClient.Value.GetSecretAsync(secretName);
            return secret.Value;
        }

        public ISecret GetSecretObject(string secretName)
        {
            return GetSecretObject(secretName, logger: null);
        }

        public ISecret GetSecretObject(string secretName, ILogger logger)
        {
            AzureSecurityKeyVaultSecret secret = _keyVaultClient.Value.GetSecret(secretName);
            return MapSecret(secretName, secret);
        }

        public async Task<ISecret> GetSecretObjectAsync(string secretName)
        {
            return await GetSecretObjectAsync(secretName, logger: null);
        }

        public async Task<ISecret> GetSecretObjectAsync(string secretName, ILogger logger)
        {
            AzureSecurityKeyVaultSecret secret = await _keyVaultClient.Value.GetSecretAsync(secretName);
            return MapSecret(secretName, secret);
        }

        private static ISecret MapSecret(string secretName, AzureSecurityKeyVaultSecret secret)
        {
            return new KeyVaultSecret(secretName, secret.Value, secret.Properties.ExpiresOn);
        }

        private SecretClient InitializeClient()
        {
            TokenCredential credential = null;

            if (_configuration.UseManagedIdentity)
            {
#if DEBUG
                credential = new DefaultAzureCredential();
#else
                credential = new ManagedIdentityCredential(_configuration.ClientId);
#endif
            }
            else if (_configuration.SendX5c)
            {
                var clientCredentialOptions = new ClientCertificateCredentialOptions
                {
                    SendCertificateChain = true
                };

                credential = new ClientCertificateCredential(_configuration.TenantId, _configuration.ClientId, _configuration.Certificate, clientCredentialOptions);

                // If we are in unit testing mode, we dont actually create a SecretClient
                if (_testMode)
                {
                    _isUsingSendx5c = true;
                    return _keyVaultClient.Value;
                }
            }
            else
            {
                credential = new ClientCertificateCredential(_configuration.TenantId, _configuration.ClientId, _configuration.Certificate);
            }

            return new SecretClient(GetKeyVaultUri(_configuration), credential);
        }

        private Uri GetKeyVaultUri(KeyVaultConfiguration keyVaultConfiguration)
        {
            var uriString = $"https://{keyVaultConfiguration.VaultName}.vault.azure.net/";
            return new Uri(uriString);
        }
    }
}

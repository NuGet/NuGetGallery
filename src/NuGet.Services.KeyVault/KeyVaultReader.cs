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

        public KeyVaultReader(KeyVaultConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _configuration = configuration;
            _keyVaultClient = new Lazy<SecretClient>(InitializeClient);
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

        public async Task<ISecret> GetSecretObjectAsync(string secretName)
        {
            return await GetSecretObjectAsync(secretName, logger: null);
        }

        public async Task<ISecret> GetSecretObjectAsync(string secretName, ILogger logger)
        {
            AzureSecurityKeyVaultSecret secret = await _keyVaultClient.Value.GetSecretAsync(secretName);
            return new KeyVaultSecret(secretName, secret.Value, secret.Properties.ExpiresOn);
        }

        private SecretClient InitializeClient()
        {
            TokenCredential credential = null;

            if (_configuration.UseManagedIdentity)
            {
                if (string.IsNullOrEmpty(_configuration.ClientId))
                {
                    credential = new DefaultAzureCredential();
                }
                else
                {
                    credential = new ManagedIdentityCredential(_configuration.ClientId);
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

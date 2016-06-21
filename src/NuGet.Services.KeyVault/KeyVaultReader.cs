// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace NuGet.Services.KeyVault
{
    public class KeyVaultReader : ISecretReader
    {
        private readonly KeyVaultConfiguration _configuration;
        private readonly string _vault;
        private readonly Lazy<KeyVaultClient> _keyVaultClient;

        public KeyVaultReader(KeyVaultConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _configuration = configuration;
            _vault = $"https://{_configuration.VaultName}.vault.azure.net:443/";
            _keyVaultClient = new Lazy<KeyVaultClient>(InitializeClient);
        }

        public async Task<string> ReadSecretAsync(string secretName)
        {
            var secret = await _keyVaultClient.Value.GetSecretAsync(_vault, secretName);
            return secret.Value;
        }

        private KeyVaultClient InitializeClient()
        {
            var certificate = FindCertificateByThumbprint(StoreName.My, StoreLocation.LocalMachine,
                _configuration.CertificateThumbprint, _configuration.ValidateCertificate);
            var clientAssertionCertificate = new ClientAssertionCertificate(_configuration.ClientId, certificate);

            return
                new KeyVaultClient(
                    (authority, resource, scope) => GetTokenAsync(clientAssertionCertificate, authority, resource));
        }

        private async Task<string> GetTokenAsync(ClientAssertionCertificate clientAssertionCertificate, string authority,
            string resource)
        {
            AuthenticationResult result = null;

            var authContext = new AuthenticationContext(authority);
            result = await authContext.AcquireTokenAsync(resource, clientAssertionCertificate);

            if (result == null)
            {
                throw new InvalidOperationException("Bearer token acquisition needed to call KeyVault service failed");
            }

            return result.AccessToken;
        }

        private static X509Certificate2 FindCertificateByThumbprint(StoreName storeName, StoreLocation storeLocation, string thumbprint, bool validationRequired)
        {
            var store = new X509Store(storeName, storeLocation);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                var col = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validationRequired);
                if (col.Count == 0)
                {
                    throw new ArgumentException("Certificate was not found in store");
                }

                return col[0];
            }
            finally
            {
                store.Close();
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Indexing;
using NuGet.Services.KeyVault;

namespace NuGet.Services.BasicSearch
{
    internal class SecretReaderFactory : ISecretReaderFactory
    {
        public const string VaultNameKey = "keyVault:VaultName";
        public const string ClientIdKey = "keyVault:ClientId";
        public const string CertificateThumbprintKey = "keyVault:CertificateThumbprint";

        public ISecretReader CreateSecretReader(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var vaultName = configuration.Get(VaultNameKey);
            ISecretReader secretReader;

            // Is key vault configured?
            if (string.IsNullOrEmpty(vaultName))
            {
                secretReader = new EmptySecretReader();
            }
            else
            {
                var clientId = configuration.Get(ClientIdKey);
                var certificateThumbprint = configuration.Get(CertificateThumbprintKey);

                // KeyVault is configured, but not all data is provided. Fail.
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(certificateThumbprint))
                {
                    throw new ArgumentException("Not all KeyVault configuration provided. " +
                                                $"Parameter: {VaultNameKey} Value: {VaultNameKey}, " +
                                                $"Parameter: {ClientIdKey} Value: {ClientIdKey}, " +
                                                $"Parameter: {CertificateThumbprintKey} Value: {certificateThumbprint}");
                }
               
                secretReader = new KeyVaultReader(new KeyVaultConfiguration(vaultName, clientId, certificateThumbprint, validateCertificate: true));
            }

            return secretReader;
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }
    }
}
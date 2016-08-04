// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Security.Cryptography.X509Certificates;
using NuGet.Services.KeyVault;

namespace NuGet.Jobs.Validation.Common
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        public const string VaultNameKey = "KeyVault:VaultName";
        public const string ClientIdKey = "KeyVault:ClientId";
        public const string CertificateThumbprintKey = "KeyVault:CertificateThumbprint";
        public const string StoreNameKey = "KeyVault:StoreName";
        public const string StoreLocationKey = "KeyVault:StoreLocation";
        public const string ValidateCertificateKey = "KeyVault:ValidateCertificate";

        public ISecretReader CreateSecretReader(IConfigurationService configurationService)
        {
            var vaultName = configurationService.Get(VaultNameKey).Result;
            ISecretReader secretReader;

            // Is key vault configured?
            if (string.IsNullOrEmpty(vaultName))
            {
                secretReader = new EmptySecretReader();
            }
            else
            {
                var clientId = configurationService.Get(ClientIdKey).Result;
                var certificateThumbprint = configurationService.Get(CertificateThumbprintKey).Result;
                var storeLocation = configurationService.Get(StoreLocationKey).Result;
                var storeName = configurationService.Get(StoreNameKey).Result;
                var validateCertificate = configurationService.Get(ValidateCertificateKey).Result;

                secretReader = new KeyVaultReader(
                    new KeyVaultConfiguration(
                        vaultName,
                        clientId,
                        certificateThumbprint,
                        (StoreName)Enum.Parse(typeof(StoreName), storeName),
                        (StoreLocation)Enum.Parse(typeof(StoreLocation), storeLocation),
                        bool.Parse(validateCertificate)));
            }

            return secretReader;
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }
    }
}
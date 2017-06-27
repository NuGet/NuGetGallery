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
                var storeLocation = (StoreLocation)Enum.Parse(typeof(StoreLocation), configurationService.Get(StoreLocationKey).Result);
                var storeName = (StoreName)Enum.Parse(typeof(StoreName), configurationService.Get(StoreNameKey).Result);
                var validateCertificate = bool.Parse(configurationService.Get(ValidateCertificateKey).Result);

                var certificate = CertificateUtility.FindCertificateByThumbprint(storeName, storeLocation, certificateThumbprint, validateCertificate);

                secretReader = new KeyVaultReader(
                    new KeyVaultConfiguration(
                        vaultName,
                        clientId,
                        certificate));
            }

            return secretReader;
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }
    }
}
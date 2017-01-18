// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Services.Configuration;
using NuGet.Services.KeyVault;

namespace NuGet.Services.BasicSearch.Configuration
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        private async Task<ISecretReader> CreateSecretReaderAsync()
        {
            var config =
                await new ConfigurationFactory(
                        new EnvironmentSettingsConfigurationProvider(CreateSecretInjector(new EmptySecretReader())))
                    .Get<BasicSearchConfiguration>();

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            
            ISecretReader secretReader;

            // Is KeyVault configured?
            if (string.IsNullOrEmpty(config.VaultName))
            {
                secretReader = new EmptySecretReader();
            }
            else
            {
                // KeyVault is configured, but not all data is provided. Fail.
                if (string.IsNullOrEmpty(config.ClientId) || string.IsNullOrEmpty(config.CertificateThumbprint))
                {
                    throw new ArgumentException("Not all KeyVault configuration provided. " +
                                                $"Parameter: {nameof(BasicSearchConfiguration.VaultName)} Value: {config.VaultName}, " +
                                                $"Parameter: {nameof(BasicSearchConfiguration.ClientId)} Value: {config.ClientId}, " +
                                                $"Parameter: {nameof(BasicSearchConfiguration.CertificateThumbprint)} Value: {config.CertificateThumbprint}, " +
                                                $"Parameter: {nameof(BasicSearchConfiguration.StoreName)} Value: {config.StoreName}, " +
                                                $"Parameter: {nameof(BasicSearchConfiguration.StoreLocation)} Value: {config.StoreLocation}, " +
                                                $"Parameter: {nameof(BasicSearchConfiguration.ValidateCertificate)} Value: {config.ValidateCertificate}");
                }

                secretReader =
                    new KeyVaultReader(new KeyVaultConfiguration(config.VaultName, config.ClientId,
                        config.CertificateThumbprint, config.StoreName,
                        config.StoreLocation, config.ValidateCertificate));
            }

            return secretReader;
        }

        public ISecretReader CreateSecretReader()
        {
            // NOTE: In this method we are using ".Result" on a function that makes KeyVault calls.
            // You should NEVER do this!
            // We can do it here because this code executes during startup, when it is not a problem.
            return CreateSecretReaderAsync().Result;
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }
    }
}
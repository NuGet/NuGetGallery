// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using NuGet.Services.KeyVault;

namespace NuGet.Jobs
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        public ISecretReader CreateSecretReader(IDictionary<string, string> settings)
        {
            if (JobConfigurationManager.TryGetArgument(settings, JobArgumentNames.VaultName) == null)
            {
                return new EmptySecretReader();
            }

            var storeName = JobConfigurationManager.TryGetArgument(settings, JobArgumentNames.StoreName);
            var storeLocation = JobConfigurationManager.TryGetArgument(settings, JobArgumentNames.StoreLocation);

            var keyVaultConfiguration =
                new KeyVaultConfiguration(
                    JobConfigurationManager.GetArgument(settings, JobArgumentNames.VaultName),
                    JobConfigurationManager.GetArgument(settings, JobArgumentNames.ClientId),
                    JobConfigurationManager.GetArgument(settings, JobArgumentNames.CertificateThumbprint),
                    storeName != null ? (StoreName)Enum.Parse(typeof(StoreName), storeName) : StoreName.My,
                    storeLocation != null ? (StoreLocation)Enum.Parse(typeof(StoreLocation), storeLocation) : StoreLocation.LocalMachine,
                    JobConfigurationManager.TryGetBoolArgument(settings, JobArgumentNames.ValidateCertificate, defaultValue: true));

            var refreshIntervalSec = JobConfigurationManager.TryGetIntArgument(settings,
                JobArgumentNames.RefreshIntervalSec) ?? CachingSecretReader.DefaultRefreshIntervalSec;

            return new CachingSecretReader(new KeyVaultReader(keyVaultConfiguration), refreshIntervalSec);
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader)
        {
            return new SecretInjector(secretReader);
        }
    }
}
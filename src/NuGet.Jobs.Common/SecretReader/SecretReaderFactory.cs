// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Services.KeyVault;

namespace NuGet.Jobs
{
    public class SecretReaderFactory : ISecretReaderFactory
    {
        public ISecretReader CreateSecterReader(IDictionary<string, string> settings)
        {
            if (JobConfigurationManager.TryGetArgument(settings, JobArgumentNames.VaultName) == null)
            {
                return new EmptySecretReader();
            }

            var keyVaultConfiguration =
                new KeyVaultConfiguration(
                    JobConfigurationManager.GetArgument(settings, JobArgumentNames.VaultName),
                    JobConfigurationManager.GetArgument(settings, JobArgumentNames.ClientId),
                    JobConfigurationManager.GetArgument(settings, JobArgumentNames.CertificateThumbprint),
                    JobConfigurationManager.TryGetBoolArgument(settings, JobArgumentNames.ValidateCertificate, fallbackEnvVariable: null, defaultValue: true));

            return new KeyVaultReader(keyVaultConfiguration);
        }

        public ISecretInjector CreateSecretInjector(ISecretReader secretReader, IDictionary<string, string> settings)
        {
            return new SecretInjector(secretReader);
        }
    }
}
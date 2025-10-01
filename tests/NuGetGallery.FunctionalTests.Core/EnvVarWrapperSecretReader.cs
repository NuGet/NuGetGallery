// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.KeyVault;

namespace NuGetGallery.FunctionalTests
{
    class EnvVarWrapperSecretReader : ISecretReader
    {
        private readonly Lazy<ISecretReader> _secretReader;

        public EnvVarWrapperSecretReader(ISecretReaderFactory factory) => _secretReader = new Lazy<ISecretReader>(factory.CreateSecretReader);
        public Task<string> GetSecretAsync(string secretName)=> GetSecretAsync(secretName, logger: null);
        public Task<ISecret> GetSecretObjectAsync(string secretName) => GetSecretObjectAsync(secretName, logger: null);

        public Task<string> GetSecretAsync(string secretName, ILogger logger)
        {
            if (TryGetFromEnvironmentVariable(secretName, logger) is string envVarValue)
            {
                return Task.FromResult(envVarValue);
            }

            return _secretReader.Value.GetSecretAsync(secretName, logger);
        }

        public Task<ISecret> GetSecretObjectAsync(string secretName, ILogger logger)
        {
            if (TryGetFromEnvironmentVariable(secretName, logger) is string envVarValue)
            {
                ISecret result = new KeyVaultSecret(secretName, envVarValue, null);
                return Task.FromResult(result);
            }

            return _secretReader.Value.GetSecretObjectAsync(secretName, logger);
        }

        private string TryGetFromEnvironmentVariable(string secretName, ILogger logger)
        {
            var message = $"Source of secret '{secretName}': ";
            var envVarValue = Environment.GetEnvironmentVariable(secretName);
            if (string.IsNullOrWhiteSpace(envVarValue))
            {
                message += "KEY VAULT";
                envVarValue = null;
            }
            else
            {
                message += "ENV VAR";
            }

            logger?.LogInformation(message);
            Console.WriteLine(message);

            return envVarValue;
        }
    }
}

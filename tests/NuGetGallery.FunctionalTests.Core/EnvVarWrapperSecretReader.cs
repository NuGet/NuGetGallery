// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Services.KeyVault;

namespace NuGetGallery.FunctionalTests
{
    class EnvVarWrapperSecretReader : ISecretReader
    {
        private readonly Lazy<ISecretReader> _secretReader;

        public EnvVarWrapperSecretReader(ISecretReaderFactory factory)
        {
            _secretReader = new Lazy<ISecretReader>(factory.CreateSecretReader);
        }

        public Task<string> GetSecretAsync(string secretName)
        {
            return GetSecretAsync(secretName, NullLogger.Instance);
        }

        public Task<string> GetSecretAsync(string secretName, ILogger logger)
        {
            if (TryGetFromEnvironmentVariable(secretName, logger) is string envVarValue)
            {
                return Task.FromResult(envVarValue);
            }

            return _secretReader.Value.GetSecretAsync(secretName, logger);
        }

        public string GetSecret(string secretName)
        {
            return GetSecret(secretName, NullLogger.Instance);
        }

        public string GetSecret(string secretName, ILogger logger)
        {
            if (TryGetFromEnvironmentVariable(secretName, logger) is string envVarValue)
            {
                return envVarValue;
            }

            return _secretReader.Value.GetSecret(secretName, logger);
        }

        public Task<ISecret> GetSecretObjectAsync(string secretName)
        {
            return GetSecretObjectAsync(secretName, NullLogger.Instance);
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

        public ISecret GetSecretObject(string secretName)
        {
            return GetSecretObject(secretName, NullLogger.Instance);
        }

        public ISecret GetSecretObject(string secretName, ILogger logger)
        {
            if (TryGetFromEnvironmentVariable(secretName, logger) is string envVarValue)
            {
                ISecret result = new KeyVaultSecret(secretName, envVarValue, null);
                return result;
            }

            return _secretReader.Value.GetSecretObject(secretName, logger);
        }

        private static string TryGetFromEnvironmentVariable(string secretName, ILogger logger)
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

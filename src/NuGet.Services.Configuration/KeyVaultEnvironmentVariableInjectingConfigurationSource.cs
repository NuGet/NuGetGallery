// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Configuration
{
    public class KeyVaultEnvironmentVariableInjectingConfigurationSource : IConfigurationSource
    {
        private readonly string _prefix;
        private readonly ISecretInjector _secretInjector;

        public KeyVaultEnvironmentVariableInjectingConfigurationSource(string prefix, ISecretInjector secretInjector)
        {
            _prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
            _secretInjector = secretInjector ?? throw new ArgumentNullException(nameof(secretInjector));
        }

        public Microsoft.Extensions.Configuration.IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            var envSource = new EnvironmentVariablesConfigurationSource
            {
                Prefix = _prefix,
            };
            var envProvider = envSource.Build(builder);

            return new KeyVaultInjectingConfigurationProvider(envProvider, _secretInjector);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Configuration
{
    public static class ConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder AddInjectedJsonFile(
            this IConfigurationBuilder configurationBuilder,
            string path,
            ISecretInjector secretInjector)
        {
            return configurationBuilder.AddInjectedJsonFile(path, secretInjector, optional: false);
        }

        public static IConfigurationBuilder AddInjectedJsonFile(
            this IConfigurationBuilder configurationBuilder,
            string path,
            ISecretInjector secretInjector,
            bool optional)
        {
            configurationBuilder = configurationBuilder ?? throw new ArgumentNullException(nameof(configurationBuilder));

            configurationBuilder.Add(new KeyVaultJsonInjectingConfigurationSource(path, secretInjector, optional));

            return configurationBuilder;
        }

        public static IConfigurationBuilder AddInjectedEnvironmentVariables(
            this IConfigurationBuilder configurationBuilder,
            string prefix,
            ISecretInjector secretInjector)
        {
            configurationBuilder = configurationBuilder ?? throw new ArgumentNullException(nameof(configurationBuilder));

            configurationBuilder.Add(new KeyVaultEnvironmentVariableInjectingConfigurationSource(prefix, secretInjector));

            return configurationBuilder;
        }

        public static IConfigurationBuilder AddInjectedInMemoryCollection(
            this IConfigurationBuilder configurationBuilder,
            IReadOnlyDictionary<string, string> dictionary,
            ISecretInjector secretInjector)
        {
            configurationBuilder = configurationBuilder ?? throw new ArgumentNullException(nameof(configurationBuilder));

            configurationBuilder.Add(new KeyVaultInMemoryCollectionInjectingConfigurationSource(dictionary, secretInjector));

            return configurationBuilder;
        }
    }
}

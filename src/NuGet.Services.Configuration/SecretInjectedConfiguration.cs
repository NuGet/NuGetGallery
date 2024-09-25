// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Configuration
{
    public class SecretInjectedConfiguration : IConfiguration
    {
        protected readonly IConfiguration _baseConfiguration;
        protected readonly ICachingSecretInjector _secretInjector;
        protected readonly ILogger _logger;

        public SecretInjectedConfiguration(
            IConfiguration baseConfiguration,
            ICachingSecretInjector secretInjector,
            ILogger logger)
        {
            _baseConfiguration = baseConfiguration ?? throw new ArgumentNullException(nameof(baseConfiguration));
            _secretInjector = secretInjector ?? throw new ArgumentNullException(nameof(secretInjector));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string this[string key]
        {
            get => ConfigurationUtility.InjectCachedSecret(_baseConfiguration[key], _secretInjector, _logger);
            set => _baseConfiguration[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() =>
            _baseConfiguration.GetChildren().Select(originalSection => new SecretInjectedConfigurationSection(originalSection, _secretInjector, _logger));


        public IChangeToken GetReloadToken() =>
            _baseConfiguration.GetReloadToken();

        public IConfigurationSection GetSection(string key) =>
            new SecretInjectedConfigurationSection(_baseConfiguration.GetSection(key), _secretInjector, _logger);
    }
}

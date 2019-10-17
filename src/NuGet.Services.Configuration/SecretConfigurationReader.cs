// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using NuGet.Services.KeyVault;

namespace NuGet.Services.Configuration
{
    public class SecretConfigurationReader : IConfigurationRoot
    {
        private IConfigurationRoot _configuration;
        private ISecretReaderFactory _secretReaderFactory;
        private Lazy<ISecretInjector> _secretInjector;

        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationProvider> Providers => throw new NotImplementedException();

        private ISecretInjector InitSecretInjector()
        {
            return _secretReaderFactory.CreateSecretInjector(_secretReaderFactory.CreateSecretReader());
        }

        private void InjectSecrets()
        {
            var configValues = _configuration.AsEnumerable();

            foreach (KeyValuePair<string, string> configPair in configValues)
            {
                if (configPair.Value != null)
                {
                    _configuration[configPair.Key] = _secretInjector.Value.InjectAsync(configPair.Value).Result;
                }
            }
        }

        public SecretConfigurationReader(IConfigurationRoot config, ISecretReaderFactory secretReaderFactory)
        {
            _configuration = config;
            _secretReaderFactory = secretReaderFactory;

            _secretInjector = new Lazy<ISecretInjector>(InitSecretInjector, isThreadSafe: false);
            InjectSecrets();
        }

        public string this[string key]
        {
            get
            {
                return _configuration[key];
            }

            set
            {
                _configuration[key] = value;
            }
        }
        
        public void Reload()
        {
            _configuration.Reload();
            InjectSecrets();
        }

        public IConfigurationSection GetSection(string key)
        {
            return _configuration.GetSection(key);
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return _configuration.GetChildren();
        }

        public IChangeToken GetReloadToken()
        {
            return _configuration.GetReloadToken();
        }
    }
}

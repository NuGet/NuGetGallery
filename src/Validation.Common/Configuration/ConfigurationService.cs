// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System;
using System.Configuration;
using System.Threading.Tasks;
using NuGet.Services.KeyVault;

namespace NuGet.Jobs.Validation.Common
{
    public class ConfigurationService : IConfigurationService
    {
        private ISecretReaderFactory _secretReaderFactory;
        private Lazy<ISecretInjector> _secretInjector;
        
        public ConfigurationService(ISecretReaderFactory secretReaderFactory)
        {
            if (secretReaderFactory == null)
            {
                throw new ArgumentNullException(nameof(secretReaderFactory));
            }

            _secretReaderFactory = secretReaderFactory;
            _secretInjector = new Lazy<ISecretInjector>(InitSecretInjector, isThreadSafe: false);
        }

        public async Task<string> Get(string key)
        {
            var value =  ConfigurationManager.AppSettings[key];

            if (!string.IsNullOrEmpty(value))
            {
                value = await _secretInjector.Value.InjectAsync(value);
            }

            return value;
        }

        private ISecretInjector InitSecretInjector()
        {
            return _secretReaderFactory.CreateSecretInjector(_secretReaderFactory.CreateSecretReader(new ConfigurationService(new EmptySecretReaderFactory())));
        }

    }
}
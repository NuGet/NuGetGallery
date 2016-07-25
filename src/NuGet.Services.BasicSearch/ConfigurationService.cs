// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Configuration;
using NuGet.Indexing;
using NuGet.Services.KeyVault;

namespace NuGet.Services.BasicSearch
{
    internal class ConfigurationService : IConfiguration
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

        public ISecretInjector InitSecretInjector()
        {
            return _secretReaderFactory.CreateSecretInjector(_secretReaderFactory.CreateSecretReader(new ConfigurationService(new EmptySecretReaderFactory())));
        }

        /// <summary>Gets a value from the configuration service.</summary>
        /// <param name="key">The configuration key to fetch the value for.</param>
        /// <returns>The value or null if the key does not exist.</returns>
        public string Get(string key)
        {
            if (key == null) throw new ArgumentNullException("key");

            string value;
            return TryGet(key, out value) ? value : null;
        }

        /// <summary>Tries to get a value from the configuration service.</summary>
        /// <param name="key">The configuration key to fetch the value for.</param>
        /// <param name="value">The value that will be set to null if the key is not found.</param>
        /// <returns>True if the value is found. False, otherwise.</returns>
        public bool TryGet(string key, out string value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            bool hasSetValue = false;
            value = null;

            // Get value from Cloud Services (if it throws, just ignore)
            try
            {
                if (SafeRoleEnvironment.IsAvailable)
                {
                    var cloudKey = key.Replace(':', '-'); // no ':' supported in cloud services
                    value = SafeRoleEnvironment.GetConfigurationSettingValue(cloudKey);
                    hasSetValue = true;
                }
            }
            catch
            {
            }

            if (!hasSetValue)
            {
                // Get value from environment/appsettings
                value = Environment.GetEnvironmentVariable(key);
                value = value ?? ConfigurationManager.AppSettings[key];
                hasSetValue = value != null;
            }

            if (hasSetValue)
            {
                // Be careful in using this in a multithreaded environment. During startup it's ok.
                // http://stackoverflow.com/questions/32594642/azure-keyvault-active-directory-acquiretokenasync-timeout-when-called-asynchrono
                value = _secretInjector.Value.InjectAsync(value).Result;
            }
            
            return hasSetValue;
        }
    }
}
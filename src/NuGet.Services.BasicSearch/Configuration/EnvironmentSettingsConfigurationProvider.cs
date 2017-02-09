// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using NuGet.Services.KeyVault;
using NuGet.Services.Configuration;

namespace NuGet.Services.BasicSearch.Configuration
{
    public class EnvironmentSettingsConfigurationProvider : ConfigurationProvider
    {
        private readonly ISecretInjector _secretInjector;
        
        public EnvironmentSettingsConfigurationProvider(ISecretInjector secretInjector)
        {
            _secretInjector = secretInjector;
        }

        private static string GetEnvironmentSettingValue(string key)
        {
            // Get value from Cloud Services (if it throws, just ignore)
            try
            {
                if (SafeRoleEnvironment.IsAvailable)
                {
                    var cloudKey = key.Replace(':', '-'); // no ':' supported in cloud services
                    return SafeRoleEnvironment.GetConfigurationSettingValue(cloudKey);
                }
            }
            catch
            {
            }

            // Get value from environment/appsettings
            var value = Environment.GetEnvironmentVariable(key) ?? ConfigurationManager.AppSettings[key];

            if (value != null)
            {
                return value;
            }

            throw new KeyNotFoundException($"{key} was not found in the environment settings.");
        }

        protected override Task<string> Get(string key)
        {
            return _secretInjector.InjectAsync(GetEnvironmentSettingValue(key));
        }
    }
}
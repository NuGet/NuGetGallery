// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Configuration;
using NuGet.Indexing;

namespace NuGet.Services.BasicSearch
{
    internal class ConfigurationService : IConfiguration
    {
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
            if (key == null) throw new ArgumentNullException("key");

            // Get value from Cloud Services (if it throws, just ignore)
            try
            {
                if (SafeRoleEnvironment.IsAvailable)
                {
                    var cloudKey = key.Replace(':', '-'); // no ':' supported in cloud services
                    value = SafeRoleEnvironment.GetConfigurationSettingValue(cloudKey);
                    return true;
                }
            }
            catch
            {
            }

            // Get value from environment
            value = Environment.GetEnvironmentVariable(key);
            if (value != null)
            {
                return true;
            }

            // Get value from AppSettings
            value = ConfigurationManager.AppSettings[key];
            if (value != null)
            {
                return true;
            }
            return false;
        }
    }
}
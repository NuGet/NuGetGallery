// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.WindowsAzure.ServiceRuntime;
using System;
using System.Configuration;

namespace NuGet.Services.Publish
{
    internal class ConfigurationService
    {
        public string Get(string key)
        {
            if (key == null) throw new ArgumentNullException("key");

            string value;
            return TryGet(key, out value) ? value : null;
        }

        public bool TryGet(string key, out string value)
        {
            if (key == null) throw new ArgumentNullException("key");
            
            // Get value from Cloud Services (if it throws, just ignore)
            try
            {
                if (SafeRoleEnvironment.IsAvailable)
                {
                    value = SafeRoleEnvironment.GetConfigurationSettingValue(key);
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
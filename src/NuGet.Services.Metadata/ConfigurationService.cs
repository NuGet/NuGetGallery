using Microsoft.WindowsAzure.ServiceRuntime;
using System;
using System.Configuration;

namespace NuGet.Services.Metadata
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
                if (RoleEnvironment.IsAvailable)
                {
                    value = RoleEnvironment.GetConfigurationSettingValue(key);
                    return true;
                }
            }
            catch
            {
                // https://www.youtube.com/watch?v=F7xB4l8_frc
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
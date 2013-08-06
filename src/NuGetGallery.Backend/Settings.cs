using System;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery.Backend
{
    public class Settings
    {
        private IDictionary<string, string> _overrideSettings = new Dictionary<string, string>();

        private CloudStorageAccount _mainStorage;
        private CloudStorageAccount _backupStorage;
        private Uri _sqlDac;

        public virtual string EnvironmentName { get { return GetSetting("EnvironmentName"); } }
        public virtual string MainConnectionString { get { return GetSetting("Sql.Primary"); } }
        public virtual string WarehouseConnectionString { get { return GetSetting("Sql.Warehouse"); } }

        public virtual bool WhatIf
        {
            get { return String.Equals("true", GetSetting("WhatIf"), StringComparison.OrdinalIgnoreCase); }
        }

        public virtual Uri SqlDac
        {
            get
            {
                return _sqlDac ??
                    (_sqlDac = new Uri(GetSetting("SqlDac")));
            }
        }

        public virtual CloudStorageAccount MainStorage
        {
            get
            {
                return _mainStorage ??
                    (_mainStorage = GetCloudStorageAccount("Storage.Primary"));
            }
        }

        public virtual CloudStorageAccount BackupStorage
        {
            get
            {
                return _backupStorage ??
                    (_backupStorage = GetCloudStorageAccount("Storage.Backup"));
            }
        }

        public Settings() : this(new Dictionary<string, string>()) { }
        public Settings(IDictionary<string, string> overrideSettings)
        {
            _overrideSettings = overrideSettings;
        }

        public virtual string GetSetting(string name)
        {
            string val;
            if (!_overrideSettings.TryGetValue(name, out val))
            {
                name = "Operations." + name;
                // Try Azure Config
                try
                {
                    if (RoleEnvironment.IsAvailable)
                    {
                        val = RoleEnvironment.GetConfigurationSettingValue(name);
                    }
                }
                catch
                {
                    val = null;
                }
                if (String.IsNullOrWhiteSpace(val))
                {
                    val = ConfigurationManager.AppSettings[name];
                }
            }
            return val;
        }

        public virtual CloudStorageAccount GetCloudStorageAccount(string name)
        {
            string setting = GetSetting(name);
            if (String.IsNullOrEmpty(setting))
            {
                return null;
            }
            return CloudStorageAccount.Parse(setting);
        }
    }
}

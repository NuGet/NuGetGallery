using System;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery.Operations.Worker
{
    public class Settings
    {
        private IDictionary<string, string> _overrideSettings = new Dictionary<string, string>();

        private CloudStorageAccount _mainStorage;
        private CloudStorageAccount _backupSourceStorage;
        private CloudStorageAccount _diagStorage;

        public virtual string EnvironmentName { get { return GetSetting("NUGET_GALLERY_ENV"); } }
        public virtual string MainConnectionString { get { return GetSetting("NUGET_GALLERY_MAIN_CONNECTION_STRING"); } }
        public virtual string BackupSourceConnectionString { get { return GetSetting("NUGET_GALLERY_BACKUP_SOURCE_CONNECTION_STRING"); } }
        public virtual string WarehouseConnectionString { get { return GetSetting("NUGET_WAREHOUSE_SQL_AZURE_CONNECTION_STRING"); } }
        public virtual string ReportsConnectionString { get { return GetSetting("NUGET_WAREHOUSE_REPORTS_STORAGE"); } }

        public virtual bool WhatIf
        {
            get { return String.Equals("true", GetSetting("WhatIf"), StringComparison.OrdinalIgnoreCase); }
        }

        public virtual CloudStorageAccount MainStorage
        {
            get
            {
                return _mainStorage ??
                    (_mainStorage = GetCloudStorageAccount("NUGET_GALLERY_MAIN_STORAGE"));
            }
        }

        public virtual CloudStorageAccount BackupSourceStorage
        {
            get
            {
                return _backupSourceStorage ??
                    (_backupSourceStorage = GetCloudStorageAccount("NUGET_GALLERY_BACKUP_SOURCE_STORAGE"));
            }
        }

        public virtual CloudStorageAccount DiagnosticsStorage
        {
            get
            {
                return _diagStorage ??
                    (_diagStorage = GetCloudStorageAccount("NUGET_GALLERY_DIAGNOSTICS_STORAGE"));
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
                val = Environment.GetEnvironmentVariable(name);
                if (String.IsNullOrWhiteSpace(val))
                {
                    val = ConfigurationManager.AppSettings[name];
                }
            }
            return val;
        }

        public virtual CloudStorageAccount GetCloudStorageAccount(string name)
        {
            return CloudStorageAccount.Parse(GetSetting(name));
        }
    }
}

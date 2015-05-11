// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
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
        private Uri _licenseReportService;
        private NetworkCredential _licenseReportCredentials;
        private string _smtpUri;

        public virtual string EnvironmentName { get { return GetSetting("Operations.EnvironmentName"); } }

        public virtual string MainConnectionString { get { return GetSetting("Operations.Sql.Primary"); } }

        public virtual string WarehouseConnectionString { get { return GetSetting("Operations.Sql.Warehouse"); } }

        public virtual bool WhatIf
        {
            get { return String.Equals("true", GetSetting("Operations.WhatIf"), StringComparison.OrdinalIgnoreCase); }
        }

        public virtual String SmtpUri
        {
            get
            {
                return _smtpUri ??
                    (_smtpUri = GetSetting("Operations.SmtpUri"));
            }
        }

        public virtual Uri SqlDac
        {
            get
            {
                return _sqlDac ??
                    (_sqlDac = new Uri(GetSetting("Operations.SqlDac")));
            }
        }

        public virtual Uri LicenseReportService
        {
            get
            {
                return _licenseReportService ??
                    (_licenseReportService = new Uri(GetSetting("Operations.LicenseReport.Service")));
            }
        }

        public virtual NetworkCredential LicenseReportCredentials
        {
            get
            {
                if (_licenseReportCredentials == null)
                {
                    string user = GetSetting("Operations.LicenseReport.User");
                    string pass = GetSetting("Operations.LicenseReport.Password");
                    _licenseReportCredentials = new NetworkCredential(user, pass);
                }
                return _licenseReportCredentials;
            }
        }

        public virtual CloudStorageAccount MainStorage
        {
            get
            {
                return _mainStorage ??
                    (_mainStorage = GetCloudStorageAccount("Operations.Storage.Primary"));
            }
        }

        public virtual CloudStorageAccount BackupStorage
        {
            get
            {
                return _backupStorage ??
                    (_backupStorage = GetCloudStorageAccount("Operations.Storage.Backup"));
            }
        }

        public virtual CloudStorageAccount DiagnosticsStorage
        {
            get
            {
                return _backupStorage ??
                    (_backupStorage = GetCloudStorageAccount("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString"));
            }
        }

        public Settings()
            : this(new Dictionary<string, string>())
        {
        }

        public Settings(IDictionary<string, string> overrideSettings)
        {
            _overrideSettings = overrideSettings;
        }

        public virtual string GetSetting(string name)
        {
            string val;
            if (!_overrideSettings.TryGetValue(name, out val))
            {
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
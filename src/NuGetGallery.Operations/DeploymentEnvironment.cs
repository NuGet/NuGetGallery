// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.WindowsAzure.Storage;
using NLog;

namespace NuGetGallery.Operations
{
    public class DeploymentEnvironment
    {
        public static readonly Logger Log = LogManager.GetLogger("DeploymentEnvironment");

        public IDictionary<string, string> Settings { get; private set; }

        public string EnvironmentName { get; private set; }
        public string SubscriptionId { get; private set; }
        public string SubscriptionName { get; private set; }

        public SqlConnectionStringBuilder MainDatabase { get; private set; }

        public SqlConnectionStringBuilder WarehouseDatabase { get; private set; }

        public CloudStorageAccount MainStorage { get; private set; }

        public CloudStorageAccount BackupStorage { get; private set; }

        public CloudStorageAccount DiagnosticsStorage { get; private set; }

        public Uri SqlDacEndpoint { get; private set; }

        public Uri LicenseReportService { get; private set; }

        public NetworkCredential LicenseReportServiceCredentials { get; private set; }

        public DeploymentEnvironment(string environmentName, string subscriptionId, string subscriptionName, IDictionary<string, string> deploymentSettings)
        {
            Settings = deploymentSettings;

            EnvironmentName = environmentName;
            SubscriptionId = subscriptionId;
            SubscriptionName = subscriptionName;

            MainDatabase = GetSqlConnectionStringBuilder("Operations.Sql.Primary");
            WarehouseDatabase = GetSqlConnectionStringBuilder("Operations.Sql.Warehouse");

            MainStorage = GetCloudStorageAccount("Operations.Storage.Primary");
            BackupStorage = GetCloudStorageAccount("Operations.Storage.Backup") ?? MainStorage;
            DiagnosticsStorage = GetCloudStorageAccount("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString") ?? MainStorage;

            SqlDacEndpoint = Get("Operations.SqlDac", str => new Uri(str, UriKind.Absolute));

            LicenseReportService = Get("Operations.LicenseReport.Service", str => new Uri(str, UriKind.Absolute));

            string licenseReportUser = Get("Operations.LicenseReport.User");
            string licenseReportPassword = Get("Operations.LicenseReport.Password");
            if (!String.IsNullOrEmpty(licenseReportUser) && !String.IsNullOrEmpty(licenseReportPassword))
            {
                LicenseReportServiceCredentials = new NetworkCredential(licenseReportUser, licenseReportPassword);
            }
        }

        private string Get(string key)
        {
            string value;
            if (!Settings.TryGetValue(key, out value))
            {
                return null;
            }
            return value;
        }

        private T Get<T>(string key, Func<string, T> thunk)
        {
            string val = Get(key);
            return String.IsNullOrEmpty(val) ? default(T) : thunk(val);
        }

        private CloudStorageAccount GetCloudStorageAccount(string key)
        {
            return Get(key, str => CloudStorageAccount.Parse(str));
        }

        private SqlConnectionStringBuilder GetSqlConnectionStringBuilder(string key)
        {
            return Get(key, str => new SqlConnectionStringBuilder(str));
        }

        private static IDictionary<string, string> BuildSettingsDictionary(XDocument doc)
        {
            XNamespace ns = XNamespace.Get("http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration");
            return (from s in doc.Element(ns + "ServiceConfiguration")
                        .Element(ns + "Role")
                        .Element(ns + "ConfigurationSettings")
                        .Elements(ns + "Setting")
                    select new KeyValuePair<string, string>(
                        s.Attribute("name").Value,
                        s.Attribute("value").Value))
                    .ToDictionary(p => p.Key, p => p.Value);
        }

        public static DeploymentEnvironment FromEnvironment()
        {
            string serviceConfig = Environment.GetEnvironmentVariable("NUGET_SERVICE_CONFIG");
            IDictionary<string, string> settings = null;
            if (!String.IsNullOrEmpty(serviceConfig) && File.Exists(serviceConfig))
            {
                try
                {
                    // Load the file
                    var doc = XDocument.Load(serviceConfig);

                    // Build a dictionary of settings
                    settings = BuildSettingsDictionary(doc);
                }
                catch(Exception ex)
                {
                    Log.ErrorException("Unable to load service config: " + serviceConfig, ex);
                }
            }

            return new DeploymentEnvironment(
                Environment.GetEnvironmentVariable("NUCMD_ENVIRONMENT_NAME"),
                Environment.GetEnvironmentVariable("NUCMD_SUBSCRIPTION_ID"),
                Environment.GetEnvironmentVariable("NUCMD_SUBSCRIPTION_NAME"),
                settings ?? new Dictionary<string, string>());
        }
    }
}
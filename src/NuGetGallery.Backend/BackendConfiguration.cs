using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery.Backend
{
    public class BackendConfiguration
    {
        public CloudStorageAccount PrimaryStorage { get; private set; }
        public CloudStorageAccount BackupStorage { get; private set; }
        public CloudStorageAccount DiagnosticsStorage { get; private set; }

        public SqlConnectionStringBuilder PrimaryDatabase { get; private set; }
        public SqlConnectionStringBuilder WarehouseDatabase { get; private set; }

        private BackendConfiguration()
        {
        }

        public static BackendConfiguration CreateEmpty()
        {
            return new BackendConfiguration();
        }

        public static BackendConfiguration Load()
        {
            var config = new BackendConfiguration();

            config.PrimaryStorage = TryGetStorageAccount("Storage.Primary");
            config.BackupStorage = TryGetStorageAccount("Storage.Backup");
            config.DiagnosticsStorage = TryGetStorageAccount("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString");

            config.PrimaryDatabase = TryGetSqlConfig("Sql.Primary");
            config.WarehouseDatabase = TryGetSqlConfig("Sql.Warehouse");
            return config;
        }

        private static CloudStorageAccount TryGetStorageAccount(string name)
        {
            string val = RoleEnvironment.GetConfigurationSettingValue(name);
            return String.IsNullOrEmpty(val) ? null : CloudStorageAccount.Parse(val);
        }

        private static SqlConnectionStringBuilder TryGetSqlConfig(string name)
        {
            string val = RoleEnvironment.GetConfigurationSettingValue(name);
            return String.IsNullOrEmpty(val) ? null : new SqlConnectionStringBuilder(val);
        }
    }
}

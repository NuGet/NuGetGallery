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
    public delegate bool TryParse<T>(string input, out T val);

    public class BackendConfiguration
    {
        private Func<string, string> _configThunk;
        
        public string InstanceId { get; private set; }

        public CloudStorageAccount PrimaryStorage { get { return GetStorageAccount("Storage.Primary"); } }
        public CloudStorageAccount BackupStorage { get { return GetStorageAccount("Storage.Backup"); } }
        public CloudStorageAccount DiagnosticsStorage { get { return GetStorageAccount("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString"); } }

        public SqlConnectionStringBuilder PrimaryDatabase { get { return GetSqlConnection("Sql.Primary"); } }
        public SqlConnectionStringBuilder WarehouseDatabase { get { return GetSqlConnection("Sql.Warehouse"); } }

        public TimeSpan QueuePollInterval { get { return Get<TimeSpan>("Queue.PollInterval", TimeSpan.TryParse, TimeSpan.FromSeconds(1)); } }

        private BackendConfiguration()
            : this(Environment.MachineName, NullThunk)
        {
        }

        private BackendConfiguration(string instanceId, Func<string, string> configThunk)
        {
            InstanceId = instanceId;
            _configThunk = configThunk;
        }

        public string Get(string key)
        {
            return _configThunk(key);
        }

        public T Get<T>(string key, Func<string, T> converter)
        {
            return Get<T>(key, converter, default(T));
        }

        public T Get<T>(string key, Func<string, T> converter, T defaultValue)
        {
            string val = Get(key);
            if (String.IsNullOrEmpty(val))
            {
                return defaultValue;
            }
            return converter(val);
        }

        public T Get<T>(string key, TryParse<T> tryConverter)
        {
            return Get<T>(key, tryConverter, default(T));
        }

        public T Get<T>(string key, TryParse<T> tryConverter, T defaultValue)
        {
            string val = Get(key);
            T ret;
            if (String.IsNullOrEmpty(val) || !tryConverter(val, out ret))
            {
                return defaultValue;
            }
            return ret;
        }

        public CloudStorageAccount GetStorageAccount(string key)
        {
            return Get<CloudStorageAccount>(key, CloudStorageAccount.TryParse);
        }

        public SqlConnectionStringBuilder GetSqlConnection(string key)
        {
            return Get(key, v => new SqlConnectionStringBuilder(v));
        }

        public static BackendConfiguration Create()
        {
            return Create(new Dictionary<string, string>());
        }

        public static BackendConfiguration Create(IDictionary<string, string> config)
        {
            return new BackendConfiguration(
                Environment.MachineName, 
                key => config.ContainsKey(key) ? config[key] : null);
        }

        public static BackendConfiguration CreateAzure()
        {
            return new BackendConfiguration(
                RoleEnvironment.CurrentRoleInstance.Id,
                key => RoleEnvironment.GetConfigurationSettingValue(key));
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

        private static string NullThunk(string key) { return String.Empty; }
    }
}

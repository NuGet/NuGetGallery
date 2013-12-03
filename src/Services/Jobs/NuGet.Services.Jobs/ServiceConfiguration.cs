using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using NuGetGallery.Storage;

namespace NuGet.Services.Jobs
{
    public delegate bool TryParse<T>(string input, out T val);

    public class ServiceConfiguration
    {
        private Func<string, string> _configThunk;
        
        public string InstanceId { get; private set; }

        public SqlConnectionStringBuilder PrimaryDatabase { get { return GetSqlConnection("Sql.Primary"); } }
        public SqlConnectionStringBuilder WarehouseDatabase { get { return GetSqlConnection("Sql.Warehouse"); } }

        public StorageHub Storage { get; private set; }

        public TimeSpan QueuePollInterval { get { return Get<TimeSpan>("Queue.PollInterval", TimeSpan.TryParse, TimeSpan.FromSeconds(1)); } }

        private ServiceConfiguration()
            : this(Environment.MachineName, NullThunk)
        {
        }

        private ServiceConfiguration(string instanceId, Func<string, string> configThunk)
        {
            InstanceId = instanceId;
            _configThunk = configThunk;

            Storage = new StorageHub(
                primary: GetStorageAccount("Storage.Primary"),
                backup: GetStorageAccount("Storage.Backup"));
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

        public static ServiceConfiguration Create()
        {
            return Create(new Dictionary<string, string>());
        }

        public static ServiceConfiguration Create(IDictionary<string, string> config)
        {
            return new ServiceConfiguration(
                Environment.MachineName,
                key => config.ContainsKey(key) ? config[key] : null);
        }

        public static ServiceConfiguration CreateAzure()
        {
            return new ServiceConfiguration(
                RoleEnvironment.CurrentRoleInstance.Id,
                key => RoleEnvironment.GetConfigurationSettingValue(key));
        }

        public SqlConnectionStringBuilder GetSqlServer(KnownSqlServer server)
        {
            switch (server)
            {
                case KnownSqlServer.Primary:
                    return PrimaryDatabase;
                case KnownSqlServer.Warehouse:
                    return WarehouseDatabase;
                default:
                    throw new InvalidOperationException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.BackendConfiguration_UnknownSqlServer,
                        server.ToString()));
            }
        }

        public CloudStorageAccount GetStorageAccount(KnownStorageAccount account)
        {
            switch (account)
            {
                case KnownStorageAccount.Primary:
                    return PrimaryStorage;
                case KnownStorageAccount.Backup:
                    return BackupStorage;
                case KnownStorageAccount.Diagnostics:
                    return DiagnosticsStorage;
                default:
                    throw new InvalidOperationException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.BackendConfiguration_UnknownStorageAccount,
                        account.ToString()));
            }
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

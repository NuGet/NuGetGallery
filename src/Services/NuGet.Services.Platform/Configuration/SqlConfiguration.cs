using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace NuGet.Services.Configuration
{
    public class SqlConfiguration : ICustomConfigurationSection
    {
        private Dictionary<KnownSqlServer, SqlConnectionStringBuilder> _servers;

        public SqlConnectionStringBuilder GetConnectionString(KnownSqlServer account)
        {
            SqlConnectionStringBuilder connectionString;
            if (!_servers.TryGetValue(account, out connectionString))
            {
                return null;
            }
            return connectionString;
        }

        public void Resolve(string prefix, ConfigurationHub hub)
        {
            _servers = Enum.GetValues(typeof(KnownSqlServer))
                .OfType<KnownSqlServer>()
                .Select(a => new KeyValuePair<KnownSqlServer, string>(a, hub.GetSetting(prefix + a.ToString())))
                .Where(p => !String.IsNullOrEmpty(p.Value))
                .ToDictionary(p => p.Key, p => new SqlConnectionStringBuilder(p.Value));
        }
    }
}

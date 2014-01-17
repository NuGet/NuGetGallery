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
        public Dictionary<KnownSqlServer, SqlConnectionStringBuilder> Connections { get; private set; }
        public Dictionary<KnownSqlServer, SqlConnectionStringBuilder> AdminConnections { get; private set; }

        public SqlConnectionStringBuilder Primary { get { return GetConnectionString(KnownSqlServer.Primary); } }
        public SqlConnectionStringBuilder Legacy { get { return GetConnectionString(KnownSqlServer.Legacy); } }
        public SqlConnectionStringBuilder Warehouse { get { return GetConnectionString(KnownSqlServer.Warehouse); } }

        public SqlConnectionStringBuilder GetConnectionString(KnownSqlServer account)
        {
            return GetConnectionString(account, admin: false);
        }

        public SqlConnectionStringBuilder GetConnectionString(KnownSqlServer account, bool admin)
        {
            SqlConnectionStringBuilder connectionString;
            if (!(admin ? AdminConnections : Connections).TryGetValue(account, out connectionString))
            {
                return null;
            }
            return connectionString;
        }

        public void Resolve(string prefix, ConfigurationHub hub)
        {
            Connections = GetConnections(hub, prefix, String.Empty);
            AdminConnections = GetConnections(hub, prefix, ".Admin");
        }

        private static Dictionary<KnownSqlServer, SqlConnectionStringBuilder> GetConnections(ConfigurationHub hub, string prefix, string suffix)
        {
            return Enum.GetValues(typeof(KnownSqlServer))
                .OfType<KnownSqlServer>()
                .Select(a => new KeyValuePair<KnownSqlServer, string>(a, hub.GetSetting(prefix + a.ToString() + suffix)))
                .Where(p => !String.IsNullOrEmpty(p.Value))
                .ToDictionary(p => p.Key, p => new SqlConnectionStringBuilder(p.Value));
        }
    }
}

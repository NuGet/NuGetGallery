using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public abstract class SqlDatabaseMonitorBase : SqlMonitorBase
    {
        public string Database { get; private set; }
        
        public SqlDatabaseMonitorBase(string server, string database, string user, string password) : base(server, user, password)
        {
            Database = database;
        }

        protected override SqlConnectionStringBuilder BuildConnectionString()
        {
            string connStr = String.Format(
                        "Server=tcp:{0};" +
                        "Database={1};" +
                        "User ID={2};" +
                        "Password={3};" +
                        "Trusted_Connection=False;" +
                        "Encrypt=True;",
                        Server,
                        Database,
                        User,
                        Password);
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connStr);
            return builder;
        }

        protected override string FormatResourceName()
        {
            return "Server=" + Server + ";Database=" + Database;
        }
    }
}

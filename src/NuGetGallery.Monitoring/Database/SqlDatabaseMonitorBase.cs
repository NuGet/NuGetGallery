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
        protected SqlDatabaseMonitorBase(string connectionString) : base(connectionString) { }
        protected SqlDatabaseMonitorBase(SqlConnectionStringBuilder connectionString) : base(connectionString) { }

        protected override string GetConnectionString()
        {
            // The base method suppresses the Initial Catalog parameter.
            return ConnectionString.ConnectionString;
        }

        protected override string FormatResourceName()
        {
            return "Server=" + ConnectionString.DataSource + ";Database=" + ConnectionString.InitialCatalog;
        }
    }
}

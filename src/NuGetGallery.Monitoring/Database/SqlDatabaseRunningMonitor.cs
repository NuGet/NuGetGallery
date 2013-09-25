using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public class SqlDatabaseRunningMonitor : SqlDatabaseMonitorBase
    {
        public SqlDatabaseRunningMonitor(SqlConnectionStringBuilder connectionString) : base(connectionString) { }
        public SqlDatabaseRunningMonitor(string connectionString) : base(connectionString) { }

        protected override Task Invoke()
        {
            // Just connect and time the connection
            return Connect(_ =>
            {
                Success("Connected to the SQL Database");
                QoS("Connected to the SQL Database", success: true, timeTaken: TimeToConnect);
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public class SqlDatabaseRunningMonitor : SqlDatabaseMonitorBase
    {
        public SqlDatabaseRunningMonitor(string server, string database, string user, string password) : base(server, database, user, password) { }

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

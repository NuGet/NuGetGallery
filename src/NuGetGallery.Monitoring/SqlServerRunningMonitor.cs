using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public class SqlServerRunningMonitor : SqlMonitorBase
    {
        public SqlServerRunningMonitor(string server, string user, string password) : base(server, user, password) { }

        protected override Task Invoke()
        {
            // Just connect and time the connection
            return Connect(_ =>
            {
                Success("Connected to the SQL Server");
                QoS("Connected to the SQL Server", success: true, timeTaken: TimeToConnect);
            });
        }
    }
}

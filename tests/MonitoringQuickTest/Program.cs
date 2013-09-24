using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Monitoring;

namespace MonitoringQuickTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleMonitorRunner.Run(new MonitorSet(
                "Tests", TimeSpan.FromSeconds(1),
                new SqlBackupMonitor(ConnectionString.Value)));
        }
    }
}

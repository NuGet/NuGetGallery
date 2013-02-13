using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Monitoring;

namespace MonitoringTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleMonitorRunner.Run("Monitors", TimeSpan.FromSeconds(5),
                new HttpMonitor("https://nuget.org", checkCertificate: true));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public class ConsoleMonitorRunner
    {
        public static void Run(string name, TimeSpan period, params ApplicationMonitor[] monitors)
        {
            Run(name, period, (IEnumerable<ApplicationMonitor>)monitors);
        }

        public static void Run(string name, TimeSpan period, IEnumerable<ApplicationMonitor> monitors)
        {
            Run(new MonitorSet(name, period, monitors));
        }

        public static void Run(MonitorSet monitorSet)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            Console.WriteLine("Starting Monitors...");

            // Start the runners
            var cancelSource = new CancellationTokenSource();
            var task = monitorSet
                .Run(new ColoredConsoleEventReporter(), cancelSource.Token)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Console.WriteLine("An exception occurred!");
                        Console.WriteLine(t.Exception.GetBaseException());
                    }
                });

            // Wait for cancellation from the user
            Console.WriteLine("Monitors Running. Press ENTER to shut down");
            Console.ReadLine();
            Console.WriteLine("Shutting down");

            // Cancel the task and wait for completion
            cancelSource.Cancel();
            task.Wait();
        }
    }
}

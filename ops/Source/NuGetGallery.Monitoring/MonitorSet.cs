using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    /// <summary>
    /// A collection of monitors to run at a specific interval
    /// </summary>
    public class MonitorSet
    {
        /// <summary>
        /// Gets the name of this monitor set
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the time between invocations of the monitor operations
        /// </summary>
        public TimeSpan Period { get; private set; }

        /// <summary>
        /// Gets the monitors to run
        /// </summary>
        public IEnumerable<ApplicationMonitor> Monitors { get; private set; }

        public MonitorSet(string name, TimeSpan period, params ApplicationMonitor[] monitors) : this(name, period, (IEnumerable<ApplicationMonitor>)monitors) { }
        public MonitorSet(string name, TimeSpan period, IEnumerable<ApplicationMonitor> monitors)
        {
            Name = name;
            Period = period;
            Monitors = monitors;
        }

        /// <summary>
        /// Starts running the monitor.
        /// </summary>
        /// <param name="reporter">An object used to report status to the underlying monitoring infrastructure</param>
        /// <param name="cancelToken">A token used to cancel the monitoring operation</param>
        /// <returns>A task that completes when the monitor shuts down (i.e. cancelToken is cancelled)</returns>
        public virtual async Task Run(IEventReporter reporter, CancellationToken cancelToken)
        {
            Trace.WriteLine(String.Format(
                "[{0}][{1}] Host Started",
                DateTime.UtcNow.ToString("HH:mm:ss.ff"),
                Name));

            while (!cancelToken.IsCancellationRequested)
            {
                Trace.WriteLine(String.Format(
                    "[{0}][{1}] Cycle Started",
                    DateTime.UtcNow.ToString("HH:mm:ss.ff"),
                    Name));

                foreach (ApplicationMonitor monitor in Monitors)
                {
                    try
                    {
                        await monitor.Invoke(reporter, cancelToken);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(String.Format(
                            "[{0}][{1}] Unhandled Monitor Exception: \n{2}",
                            DateTime.UtcNow.ToString("HH:mm:ss.ff"),
                            Name,
                            ex));
                    }
                }

                Trace.WriteLine(String.Format(
                    "[{0}][{1}] Cycle Complete, Sleeping for {2}",
                    DateTime.UtcNow.ToString("HH:mm:ss.ff"),
                    Name,
                    Period));

                // Wait until the next period
                await TaskEx.Delay(Period, cancelToken);
            }
        }
    }
}

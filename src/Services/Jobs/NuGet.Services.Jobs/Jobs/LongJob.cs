using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs
{
    /// <summary>
    /// Job used to confirm the worker is responding to requests
    /// </summary>
    public class LongJob : Job<LongEventSource>
    {
        protected internal override async Task Execute()
        {
            // Extend the message lease to 10mins from now
            await Extend(TimeSpan.FromMinutes(10));

            // Sleep for a minute and report that we're still running
            await Task.Delay(TimeSpan.FromMinutes(1));
            Log.StillRunning();

            // Sleep for a minute and report that we're still running
            await Task.Delay(TimeSpan.FromMinutes(1));
            Log.StillRunning();

            // Sleep for a minute and report that we're still running
            await Task.Delay(TimeSpan.FromMinutes(1));
            Log.StillRunning();

            // Sleep for a minute and report that we're still running
            await Task.Delay(TimeSpan.FromMinutes(1));
            Log.StillRunning();

            // Sleep for a minute and report that we're still running
            await Task.Delay(TimeSpan.FromMinutes(1));
            Log.StillRunning();

            // Sleep for a minute and report that we're still running
            await Task.Delay(TimeSpan.FromMinutes(1));
            Log.StillRunning();
        }
    }

    [EventSource(Name = "NuGet-Jobs-Long")]
    public class LongEventSource : EventSource
    {
        [Event(
            eventId: 1,
            Message = "Still running")]
        public void StillRunning() { WriteEvent(1); }
    }
}

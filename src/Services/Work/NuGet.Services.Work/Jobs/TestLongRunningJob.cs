using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Jobs
{
    /// <summary>
    /// Job used to confirm the worker is responding to requests
    /// </summary>
    [Description("A simple long-running job for testing")]
    public class TestLongRunningJob : JobHandler<TestLongRunningEventSource>
    {
        protected internal override async Task Execute()
        {
            // Extend the message lease to 10mins from now
            await Extend(TimeSpan.FromMinutes(10));

            // Sleep for a minute and report that we're still running
            await Task.Delay(TimeSpan.FromMinutes(1));
            Log.StillRunning();

            await Task.Delay(TimeSpan.FromSeconds(10 * new Random().Next(1, 5)));
            Log.StillRunning();
        }
    }

    [EventSource(Name="Outercurve-NuGet-Jobs-TestLongRunning")]
    public class TestLongRunningEventSource : EventSource
    {
        public static readonly TestLongRunningEventSource Log = new TestLongRunningEventSource();
        private TestLongRunningEventSource() { }

        [Event(
            eventId: 1,
            Message = "Still running")]
        public void StillRunning() { WriteEvent(1); }
    }
}

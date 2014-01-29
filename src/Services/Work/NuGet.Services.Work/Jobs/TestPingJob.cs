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
    [Description("A simple one-time job for testing")]
    public class TestPingJob : JobHandler<TestPingEventSource>
    {
        protected internal override Task Execute()
        {
            Log.Pong();
            return Task.FromResult<object>(null);
        }
    }

    [EventSource(Name="Outercurve-NuGet-Jobs-TestPing")]
    public class TestPingEventSource : EventSource
    {
        public static readonly TestPingEventSource Log = new TestPingEventSource();
        private TestPingEventSource() { }

        [Event(
            eventId: 1,
            Message = "Pong")]
        public void Pong() { WriteEvent(1); }
    }
}

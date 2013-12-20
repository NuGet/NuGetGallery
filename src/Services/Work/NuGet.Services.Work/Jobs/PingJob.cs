using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Jobs
{
    /// <summary>
    /// Job used to confirm the worker is responding to requests
    /// </summary>
    public class PingJob : Job<PingEventSource>
    {
        protected internal override Task Execute()
        {
            Log.Pong();
            return Task.FromResult<object>(null);
        }
    }

    [EventSource(Name = "NuGet-Jobs-Ping")]
    public class PingEventSource : EventSource
    {
        public static PingEventSource Log = new PingEventSource();
        private PingEventSource() { }

        [Event(
            eventId: 1,
            Message = "Pong")]
        public void Pong() { WriteEvent(1); }
    }
}

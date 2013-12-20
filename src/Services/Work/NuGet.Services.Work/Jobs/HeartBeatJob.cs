using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Jobs
{
    /// <summary>
    /// Job used to confirm that the worker remains active
    /// </summary>
    public class HeartBeatJob : RepeatingJob<HeartBeatEventSource>
    {
        public override TimeSpan WaitPeriod
        {
            get { return TimeSpan.FromMinutes(5); }
        }

        protected internal override Task Execute()
        {
            Log.Thump();
            return Task.FromResult<object>(null);
        }
    }

    [EventSource(Name = "NuGet-Jobs-HeartBeat")]
    public class HeartBeatEventSource : EventSource
    {
        public static HeartBeatEventSource Log = new HeartBeatEventSource();
        private HeartBeatEventSource() { }

        [Event(
            eventId: 1,
            Message = "Thump! Worker is still active!")]
        public void Thump() { WriteEvent(1); }
    }
}

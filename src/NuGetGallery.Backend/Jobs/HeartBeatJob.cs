using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend.Jobs
{
    public class HeartBeatJob : RepeatingJob<HeartBeatEventSource>
    {
        public override TimeSpan WaitPeriod
        {
            get { return TimeSpan.FromSeconds(10); }
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

        public void Thump() { WriteEvent(1); }
    }
}

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
    /// Job used to confirm that the worker remains active
    /// </summary>
    [Description("A simple heart-beat job for testing")]
    public class TestHeartBeatJob : RepeatingJobHandler<TestHeartBeatEventSource>
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

    [EventSource(Name="Outercurve-NuGet-Jobs-TestHeartBeat")]
    public class TestHeartBeatEventSource : EventSource
    {
        public static readonly TestHeartBeatEventSource Log = new TestHeartBeatEventSource();
        private TestHeartBeatEventSource() { }

        [Event(
            eventId: 1,
            Message = "Thump! Worker is still active!")]
        public void Thump() { WriteEvent(1); }
    }
}

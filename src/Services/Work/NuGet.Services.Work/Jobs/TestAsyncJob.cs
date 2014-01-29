using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Work.Jobs
{
    [Description("A simple async job for testing")]
    public class TestAsyncJob : AsyncJobHandler<TestAsyncEventSource>
    {
        public string Message { get; set; }

        protected internal override Task<JobContinuation> Execute()
        {
            Log.Started();
            Log.Suspending();
            return Task.FromResult(Suspend(TimeSpan.FromMinutes(1), new Dictionary<string, string>() {
                {"Message", "Hello!"}
            }));
        }

        protected internal override async Task<JobContinuation> Resume()
        {
            Log.Back(Message);
            await Task.Delay(TimeSpan.FromSeconds(30));
            return Complete();
        }
    }

    [EventSource(Name="Outercurve-NuGet-Jobs-TestAsync")]
    public class TestAsyncEventSource : EventSource
    {
        public static readonly TestAsyncEventSource Log = new TestAsyncEventSource();
        private TestAsyncEventSource() { }

        [Event(
            eventId: 1,
            Message = "Started!")]
        public void Started() { WriteEvent(1); }

        [Event(
            eventId: 2,
            Message = "Suspending!")]
        public void Suspending() { WriteEvent(2); }

        [Event(
            eventId: 3,
            Message = "Back! With Message: {0}")]
        public void Back(string message) { WriteEvent(3, message); }
    }
}

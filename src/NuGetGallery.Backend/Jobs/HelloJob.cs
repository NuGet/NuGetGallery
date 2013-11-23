using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Jobs;

namespace NuGetGallery.Backend.Jobs
{
    public class HelloJob : AsyncJob<HelloEventSource>
    {
        public string Message { get; set; }

        protected internal override Task<JobContinuation> Execute()
        {
            Log.Started();
            Log.Saying("Hello, " + Message + "!");

            return Continue(TimeSpan.FromMinutes(1), new Dictionary<string, string>()
            {
                {"Message", "World"}
            });
        }

        protected internal override Task<JobContinuation> Continue()
        {
            Log.Continuing(Message);
            Log.Finished();
            
            return Complete(); // Done!
        }
    }

    [EventSource(Name = "NuGet-Jobs-Hello")]
    public class HelloEventSource : EventSource
    {
        public void Started() { WriteEvent(1); }
        public void Saying(string message) { WriteEvent(2, message); }
        public void Finished() { WriteEvent(3); }
        public void Continuing(string message) { WriteEvent(4, message); }
    }
}

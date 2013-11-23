using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend.Jobs
{
    public class HelloJob : Job<HelloEventSource>
    {
        protected internal override async Task Execute()
        {
            Log.Started();
            await Task.Delay(500);
            Log.Saying("Hello!");
            await Task.Delay(500);
            Log.Finished();
        }
    }

    [EventSource(Name = "NuGet-Jobs-Hello")]
    public class HelloEventSource : EventSource
    {
        public void Started() { WriteEvent(1); }
        public void Saying(string message) { WriteEvent(2, message); }
        public void Finished() { WriteEvent(3); }
    }
}

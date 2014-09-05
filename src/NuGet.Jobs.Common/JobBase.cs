using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;

namespace NuGet.Jobs.Common
{
    public abstract class JobBase
    {
        public JobBase() : this(null) { }
        public JobBase(EventSource jobEventSource)
        {
            JobName = this.GetType().ToString();
            // Setup the logger. If this fails, don't catch it
            Logger = new JobTraceLogger(JobName);

            if(jobEventSource != null)
            {
                Listener = new JobTraceEventListener(Logger);
                Listener.EnableEvents(jobEventSource, EventLevel.LogAlways);
            }
        }

        public string JobName { get; protected set; }

        public JobTraceLogger Logger { get; protected set; }

        public JobTraceEventListener Listener { get; protected set; }

        public abstract bool Init(IDictionary<string, string> jobArgsDictionary);

        public abstract Task Run();
    }
}

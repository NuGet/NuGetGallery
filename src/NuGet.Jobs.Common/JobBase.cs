using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;

namespace NuGet.Jobs.Common
{
    public abstract class JobBase
    {
        private EventSource JobEventSource { get; set; }
        public JobBase() : this(null) { }
        public JobBase(EventSource jobEventSource)
        {
            JobName = this.GetType().ToString();
            JobEventSource = jobEventSource;
        }

        public string JobName { get; protected set; }

        public JobTraceLogger Logger { get; protected set; }

        public JobTraceEventListener Listener { get; protected set; }

        public void SetLogger(JobTraceLogger logger)
        {
            Logger = logger;

            if(Listener != null)
            {
                Listener.Dispose();
            }

            if(JobEventSource != null)
            {
                Listener = new JobTraceEventListener(Logger);
                Listener.EnableEvents(JobEventSource, EventLevel.LogAlways);
            }
        }

        public abstract bool Init(IDictionary<string, string> jobArgsDictionary);

        public abstract Task<bool> Run();
    }
}

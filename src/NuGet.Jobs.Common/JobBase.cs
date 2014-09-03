using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Jobs.Common
{
    public abstract class JobBase
    {
        public JobBase()
        {
            JobName = this.GetType().ToString();
            // Setup the logger. If this fails, don't catch it
            Logger = new JobTraceLogger(JobName);
        }

        public string JobName { get; protected set; }

        public JobTraceLogger Logger { get; protected set; }

        public abstract bool Init(IDictionary<string, string> jobArgsDictionary);

        public abstract Task Run();
    }
}

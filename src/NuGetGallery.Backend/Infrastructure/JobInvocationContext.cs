using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGetGallery.Backend.Monitoring;
using NuGetGallery.Jobs;

namespace NuGetGallery.Backend
{
    public class JobInvocationContext
    {
        public JobInvocation Invocation { get; private set; }
        public BackendConfiguration Config { get; private set; }
        public BackendMonitoringHub Monitor { get; private set; }

        public JobInvocationContext(JobInvocation invocation, BackendConfiguration config, BackendMonitoringHub monitor)
        {
            Invocation = invocation;
            Config = config;
            Monitor = monitor;
        }
    }
}

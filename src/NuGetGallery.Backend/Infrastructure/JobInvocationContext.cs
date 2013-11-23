using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using NuGetGallery.Backend.Monitoring;
using NuGetGallery.Jobs;

namespace NuGetGallery.Backend
{
    public class JobInvocationContext
    {
        private const string InvocationIdDataName = "_NuGet_Backend_Invocation_Id";

        public JobInvocation Invocation { get; private set; }
        public BackendConfiguration Config { get; private set; }
        public BackendMonitoringHub Monitor { get; private set; }
        
        public static Guid GetCurrentInvocationId()
        {
            var obj = CallContext.LogicalGetData(InvocationIdDataName);
            return obj == null ? Guid.Empty : (Guid)obj;
        }

        public static void SetCurrentInvocationId(Guid id)
        {
            CallContext.LogicalSetData(InvocationIdDataName, id);
        }

        public JobInvocationContext(JobInvocation invocation, BackendConfiguration config, BackendMonitoringHub monitor)
        {
            Invocation = invocation;
            Config = config;
            Monitor = monitor;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using NuGet.Services.Jobs.Monitoring;

namespace NuGet.Services.Jobs
{
    public class InvocationContext
    {
        private const string InvocationIdDataName = "_NuGet_Services_Jobs_Invocation_Id";

        public Invocation Invocation { get; private set; }
        public ServiceConfiguration Config { get; private set; }
        public InvocationLogCapture LogCapture { get; private set; }
        public InvocationQueue Queue { get; private set; }
        
        public static Guid GetCurrentInvocationId()
        {
            var obj = CallContext.LogicalGetData(InvocationIdDataName);
            return obj == null ? Guid.Empty : (Guid)obj;
        }

        public static void SetCurrentInvocationId(Guid id)
        {
            CallContext.LogicalSetData(InvocationIdDataName, id);
        }

        public InvocationContext(Invocation invocation, InvocationQueue queue, ServiceConfiguration config, InvocationLogCapture logCapture)
        {
            Invocation = invocation;
            Config = config;
            Queue = queue;
            LogCapture = logCapture;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend.Tracing
{
    [EventSource(Name="NuGet-Backend-Dispatcher")]
    internal class DispatcherEventSource : EventSource
    {
        public static readonly DispatcherEventSource Log = new DispatcherEventSource();

        private DispatcherEventSource() {}

#pragma warning disable 0618
        [Event(
            eventId: 1,
            Message="{0} job discovered. Runtime: {1}",
            Level=EventLevel.Informational)]
        [Obsolete("This method supports ETL infrastructure. Use JobDiscovered(Job) instead")]
        public void JobDiscovered(string jobName, string runtime) { WriteEvent(1, jobName, runtime); }

        [NonEvent]
        public void JobDiscovered(Job instance) { JobDiscovered(instance.Name, instance.GetType().AssemblyQualifiedName); }

        [Event(
            eventId: 2,
            Message = "Dispatching Invocation of {0}. Id: {1}",
            Level = EventLevel.Informational)]
        [Obsolete("This method supports ETL infrastructure. Use Dispatching(JobInvocation) instead")]
        public void Dispatching(string jobName, string invocationId) { WriteEvent(2, jobName, invocationId); }

        [NonEvent]
        public void Dispatching(JobInvocation invocation) { Dispatching(invocation.Request.Name, invocation.Id.ToString("N")); }
#pragma warning restore 0618
    }
}

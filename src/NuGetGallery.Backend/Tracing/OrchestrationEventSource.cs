using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Backend.Tracing
{
    [EventSource(Name="NuGet-Backend-Orchestration")]
    internal class OrchestrationEventSource : EventSource
    {
        public static readonly OrchestrationEventSource Log = new OrchestrationEventSource();

        private OrchestrationEventSource() {}

        [Event(
            eventId: 1,
            Message="{0} job discovered. Runtime: {1}",
            Level=EventLevel.Informational)]
        public void JobDiscovered(string jobName, string runtime) { WriteEvent(1, jobName, runtime); }

        [NonEvent]
        public void JobDiscovered(Job instance) { JobDiscovered(instance.Name, instance.GetType().AssemblyQualifiedName); }
    }
}

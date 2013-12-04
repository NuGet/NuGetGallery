using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services
{
    [EventSource(Name = "NuGet-Services")]
    public class ServicePlatformEventSource : EventSource
    {
        public static readonly ServicePlatformEventSource Log = new ServicePlatformEventSource();
        private ServicePlatformEventSource() { }

        [Event(
            eventId: 1,
            Level = EventLevel.Informational,
            Task = Tasks.Startup,
            Opcode = EventOpcode.Start,
            Message = "{0}/{1}: Starting")]
        public void Starting(string serviceName, string instance) { WriteEvent(1, serviceName, instance); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Task = Tasks.Startup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Started")]
        public void Started(string serviceName, string instance) { WriteEvent(2, serviceName, instance); }

        [Event(
            eventId: 3,
            Level = EventLevel.Informational,
            Task = Tasks.Execution,
            Opcode = EventOpcode.Start,
            Message = "{0}/{1}: Running")]
        public void Running(string serviceName, string instance) { WriteEvent(3, serviceName, instance); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Task = Tasks.Execution,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Stopped all services on host")]
        public void Finished(string host) { WriteEvent(4, host); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Task = Tasks.Startup,
            Opcode = EventOpcode.Start,
            Message = "{0}: Stopping all services on host")]
        public void Stopping(string host) { WriteEvent(5, host); }

        [Event(
            eventId: 6,
            Level = EventLevel.Informational,
            Task = Tasks.Startup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Stopped")]
        public void Stopped(string serviceName, string instance) { WriteEvent(6, serviceName, instance); }

        [Event(
            eventId: 7,
            Level = EventLevel.Critical,
            Message = "{0}/{1}: Missing Required Endpoint '{2}'")]
        public void MissingEndpoint(string serviceName, string instance, string endpoint) { WriteEvent(7, serviceName, instance, endpoint); }

        [Event(
            eventId: 8,
            Level = EventLevel.Informational,
            Task = Tasks.HttpStartup,
            Opcode = EventOpcode.Start,
            Message = "{0}/{1}: Starting HTTP Services on port {2}")]
        public void StartingHttpServices(string serviceName, string instance, int port) { WriteEvent(8, serviceName, instance, port); }

        [Event(
            eventId: 9,
            Level = EventLevel.Informational,
            Task = Tasks.HttpStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Started HTTP Services on port {2}")]
        public void StartedHttpServices(string serviceName, string instance, int port) { WriteEvent(9, serviceName, instance, port); }

        public static class Tasks {
            public const EventTask Startup = (EventTask)0x01;
            public const EventTask Shutdown = (EventTask)0x02;
            public const EventTask Execution = (EventTask)0x03;
            public const EventTask HttpStartup = (EventTask)0x04;
        }
    }
}

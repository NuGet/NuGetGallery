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
            Task = Tasks.HostStartup,
            Opcode = EventOpcode.Start,
            Message = "{0}: Starting")]
        public void HostStarting(string hostName) { WriteEvent(1, hostName); }

        [Event(
            eventId: 2,
            Level = EventLevel.Informational,
            Task = Tasks.HostStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Started")]
        public void HostStarted(string hostName) { WriteEvent(2, hostName); }

        [Event(
            eventId: 3,
            Level = EventLevel.Critical,
            Task = Tasks.HostStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Failed to start with exception: {1}")]
        private void HostStartupFailed(string hostName, string exception) { WriteEvent(3, hostName, exception); }
        [NonEvent]
        public void HostStartupFailed(string hostName, Exception exception) { HostStartupFailed(hostName, exception.ToString()); }

        [Event(
            eventId: 4,
            Level = EventLevel.Informational,
            Task = Tasks.ServiceInitialization,
            Opcode = EventOpcode.Start,
            Message = "{0}: Initializing an instance of the service: {1}")]
        private void ServiceInitializing(string hostName, string serviceTypeName, string serviceTypeFullName) { WriteEvent(4, hostName, serviceTypeName, serviceTypeFullName); }
        [NonEvent]
        public void ServiceInitializing(string hostName, Type serviceType) { ServiceInitializing(hostName, serviceType.Name, serviceType.AssemblyQualifiedName); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Task = Tasks.ServiceInitialization,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Initialized an instance of the service: {1}")]
        private void ServiceInitialized(string hostName, string serviceTypeName, string serviceTypeFullName) { WriteEvent(5, hostName, serviceTypeName, serviceTypeFullName); }
        [NonEvent]
        public void ServiceInitialized(string hostName, Type serviceType) { ServiceInitialized(hostName, serviceType.Name, serviceType.AssemblyQualifiedName); }

        [Event(
            eventId: 6,
            Level = EventLevel.Critical,
            Task = Tasks.ServiceInitialization,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Failed to initialize an instance of the service: {2}. Exception: {1}")]
        private void ServiceInitializationFailed(string hostName, string exception, string serviceTypeName, string serviceTypeFullName) { WriteEvent(6, hostName, serviceTypeName, serviceTypeFullName); }
        [NonEvent]
        public void ServiceInitializationFailed(string hostName, Exception exception, Type serviceType) { ServiceInitializationFailed(hostName, exception.ToString(), serviceType.Name, serviceType.AssemblyQualifiedName); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Task = Tasks.ServiceStartup,
            Opcode = EventOpcode.Start,
            Message = "{0}/{1}: Starting")]
        public void ServiceStarting(string hostName, string serviceName) { WriteEvent(7, hostName, serviceName); }

        [Event(
            eventId: 8,
            Level = EventLevel.Informational,
            Task = Tasks.ServiceStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Started")]
        public void ServiceStarted(string hostName, string serviceName) { WriteEvent(8, hostName, serviceName); }

        [Event(
            eventId: 9,
            Level = EventLevel.Critical,
            Task = Tasks.ServiceStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Failed to start with exception: {2}")]
        private void ServiceStartupFailed(string hostName, string serviceName, string exception) { WriteEvent(9, hostName, serviceName, exception); }
        [NonEvent]
        public void ServiceStartupFailed(string hostName, string serviceName, Exception exception) { ServiceStartupFailed(hostName, serviceName, exception.ToString()); }


        [Event(
            eventId: 10,
            Level = EventLevel.Critical,
            Message = "{0}/{1}: Missing Required Endpoint '{2}'")]
        public void MissingEndpoint(string hostName, string serviceName, string endpoint) { WriteEvent(10, hostName, serviceName, endpoint); }

        [Event(
            eventId: 11,
            Level = EventLevel.Informational,
            Task = Tasks.HttpStartup,
            Opcode = EventOpcode.Start,
            Message = "{0}/{1}: Starting HTTP Services on port {2}")]
        public void StartingHttpServices(string hostName, string serviceName, int port) { WriteEvent(11, hostName, serviceName, port); }

        [Event(
            eventId: 12,
            Level = EventLevel.Informational,
            Task = Tasks.HttpStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Started HTTP Services on port {2}")]
        public void StartedHttpServices(string hostName, string serviceName, int port) { WriteEvent(12, hostName, serviceName, port); }

        [Event(
            eventId: 13,
            Level = EventLevel.Critical,
            Task = Tasks.HttpStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Error Starting HTTP Services")]
        private void ErrorStartingHttpServices(string hostName, string serviceName, string exception) { WriteEvent(13, hostName, serviceName, exception); }
        [NonEvent]
        public void ErrorStartingHttpServices(string hostName, string serviceName, Exception exception) { ErrorStartingHttpServices(hostName, serviceName, exception.ToString()); }

        [Event(
            eventId: 14,
            Level = EventLevel.Informational,
            Task = Tasks.HostShutdown,
            Opcode = EventOpcode.Start,
            Message = "{0}: Host Shutdown Requested")]
        public void HostShutdownRequested(string hostName) { WriteEvent(14, hostName); }

        [Event(
            eventId: 15,
            Level = EventLevel.Informational,
            Task = Tasks.HostShutdown,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Host Shutdown Complete")]
        public void HostShutdownComplete(string hostName) { WriteEvent(15, hostName); }

        [Event(
            eventId: 16,
            Level = EventLevel.Critical,
            Message = "A fatal unhandled exception occurred: {0}")]
        private void FatalException(string exception) { WriteEvent(16, exception); }
        [NonEvent]
        public void FatalException(Exception ex) { FatalException(ex.ToString()); }

        public static class Tasks {
            public const EventTask HostStartup = (EventTask)0x01;
            public const EventTask ServiceInitialization = (EventTask)0x02;
            public const EventTask ServiceStartup = (EventTask)0x03;
            public const EventTask HttpStartup = (EventTask)0x04;
            public const EventTask HostShutdown = (EventTask)0x05;
        }
    }
}

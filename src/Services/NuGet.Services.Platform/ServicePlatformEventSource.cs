using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.ServiceModel;

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
            Message = "{0}/{1}: Initializing")]
        private void ServiceInitializing(string hostName, string instanceName) { WriteEvent(4, hostName, instanceName); }
        [NonEvent]
        public void ServiceInitializing(ServiceInstanceName name) { ServiceInitializing(name.Host.ToString(), name.ShortName); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Task = Tasks.ServiceInitialization,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Initialized")]
        private void ServiceInitialized(string hostName, string instanceName) { WriteEvent(5, hostName, instanceName); }
        [NonEvent]
        public void ServiceInitialized(ServiceInstanceName name) { ServiceInitialized(name.Host.ToString(), name.ShortName); }

        [Event(
            eventId: 6,
            Level = EventLevel.Critical,
            Task = Tasks.ServiceInitialization,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Initialization failed. Exception: {2}")]
        private void ServiceInitializationFailed(string hostName, string serviceName, string exception) { WriteEvent(6, hostName, serviceName, exception); }
        [NonEvent]
        public void ServiceInitializationFailed(ServiceInstanceName name, Exception ex) { ServiceInitializationFailed(name.Host.ToString(), name.ShortName, ex.ToString()); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Task = Tasks.ServiceStartup,
            Opcode = EventOpcode.Start,
            Message = "{0}/{1}: Starting")]
        private void ServiceStarting(string hostName, string instanceName) { WriteEvent(7, hostName, instanceName); }
        [NonEvent]
        public void ServiceStarting(ServiceInstanceName name) { ServiceStarting(name.Host.ToString(), name.ShortName); }

        [Event(
            eventId: 8,
            Level = EventLevel.Informational,
            Task = Tasks.ServiceStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Started")]
        public void ServiceStarted(string hostName, string instanceName) { WriteEvent(8, hostName, instanceName); }
        [NonEvent]
        public void ServiceStarted(ServiceInstanceName name) { ServiceStarted(name.Host.ToString(), name.ShortName); }

        [Event(
            eventId: 9,
            Level = EventLevel.Critical,
            Task = Tasks.ServiceStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Failed to start with exception: {2}")]
        private void ServiceStartupFailed(string hostName, string instanceName, string exception) { WriteEvent(9, hostName, instanceName, exception); }
        [NonEvent]
        public void ServiceStartupFailed(ServiceInstanceName name, Exception ex) { ServiceStartupFailed(name.Host.ToString(), name.ShortName, ex.ToString()); }

        [Event(
            eventId: 10,
            Level = EventLevel.Critical,
            Message = "{0}/{1}: Missing HTTP Endpoints. 'http' and/or 'https' must be provided to run HTTP services.")]
        private void MissingHttpEndpoints(string hostName, string instanceName) { WriteEvent(10, hostName, instanceName); }
        [NonEvent]
        public void MissingHttpEndpoints(ServiceInstanceName name) { MissingHttpEndpoints(name.Host.ToString(), name.ShortName); }

        [Event(
            eventId: 11,
            Level = EventLevel.Informational,
            Task = Tasks.HttpStartup,
            Opcode = EventOpcode.Start,
            Message = "{0}/{1}: Starting HTTP Services. http {0}, https {1}")]
        private void StartingHttpServices(string hostName, string instanceName, string http, string https) { WriteEvent(11, hostName, instanceName, http, https); }
        [NonEvent]
        public void StartingHttpServices(ServiceInstanceName name, IPEndPoint http, IPEndPoint https) { StartingHttpServices(name.Host.ToString(), name.ShortName, http == null ? "<disabled>" : ("on port " + http.Port.ToString()), https == null ? "<disabled>" : ("on port " + https.Port.ToString())); }

        [Event(
            eventId: 12,
            Level = EventLevel.Informational,
            Task = Tasks.HttpStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Started HTTP Services")]
        private void StartedHttpServices(string hostName, string instanceName) { WriteEvent(12, hostName, instanceName); }
        [NonEvent]
        public void StartedHttpServices(ServiceInstanceName name) { StartedHttpServices(name.Host.ToString(), name.ShortName); }

        [Event(
            eventId: 13,
            Level = EventLevel.Critical,
            Task = Tasks.HttpStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Error Starting HTTP Services")]
        private void ErrorStartingHttpServices(string hostName, string instanceName, string exception) { WriteEvent(13, hostName, instanceName, exception); }
        [NonEvent]
        public void ErrorStartingHttpServices(ServiceInstanceName name, Exception ex) { ErrorStartingHttpServices(name.Host.ToString(), name.ShortName, ex.ToString()); }

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

        [Event(
            eventId: 17,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Start,
            Task = Tasks.ServiceExecution,
            Message = "{0}/{1}: Running")]
        private void ServiceRunning(string hostName, string instanceName) { }
        [NonEvent]
        public void ServiceRunning(ServiceInstanceName name) { ServiceRunning(name.Host.ToString(), name.ShortName); }

        [Event(
            eventId: 18,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.ServiceExecution,
            Message = "{0}/{1}: Stopped")]
        private void ServiceStoppedRunning(string hostName, string instanceName) { }
        [NonEvent]
        public void ServiceStoppedRunning(ServiceInstanceName name) { ServiceStoppedRunning(name.Host.ToString(), name.ShortName); }

        [Event(
            eventId: 19,
            Level = EventLevel.Critical,
            Opcode = EventOpcode.Stop,
            Task = Tasks.ServiceExecution,
            Message = "{0}/{1}: Exception during execution: {2}")]
        private void ServiceException(string hostName, string instanceName, string exception) { WriteEvent(19, hostName, instanceName, exception); }
        [NonEvent]
        public void ServiceException(ServiceInstanceName name, Exception ex) { ServiceException(name.Host.ToString(), name.ShortName, ex.ToString()); }

        public static class Tasks {
            public const EventTask HostStartup = (EventTask)0x01;
            public const EventTask ServiceInitialization = (EventTask)0x02;
            public const EventTask ServiceStartup = (EventTask)0x03;
            public const EventTask ServiceExecution = (EventTask)0x04;
            public const EventTask HttpStartup = (EventTask)0x05;
            public const EventTask HostShutdown = (EventTask)0x06;
        }
    }
}

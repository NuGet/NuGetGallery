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
    [EventSource(Name="Outercurve-NuGet-Services")]
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
        private void ServiceInitializing(string hostName, string serviceName) { WriteEvent(4, hostName, serviceName); }
        [NonEvent]
        public void ServiceInitializing(ServiceName name) { ServiceInitializing(name.Host.ToString(), name.Service); }

        [Event(
            eventId: 5,
            Level = EventLevel.Informational,
            Task = Tasks.ServiceInitialization,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Initialized")]
        private void ServiceInitialized(string hostName, string serviceName) { WriteEvent(5, hostName, serviceName); }
        [NonEvent]
        public void ServiceInitialized(ServiceName name) { ServiceInitialized(name.Host.ToString(), name.Service); }

        [Event(
            eventId: 6,
            Level = EventLevel.Critical,
            Task = Tasks.ServiceInitialization,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Initialization failed. Exception: {2}")]
        private void ServiceInitializationFailed(string hostName, string serviceName, string exception) { WriteEvent(6, hostName, serviceName, exception); }
        [NonEvent]
        public void ServiceInitializationFailed(ServiceName name, Exception ex) { ServiceInitializationFailed(name.Host.ToString(), name.Service, ex.ToString()); }

        [Event(
            eventId: 7,
            Level = EventLevel.Informational,
            Task = Tasks.ServiceStartup,
            Opcode = EventOpcode.Start,
            Message = "{0}/{1}: Starting")]
        private void ServiceStarting(string hostName, string serviceName) { WriteEvent(7, hostName, serviceName); }
        [NonEvent]
        public void ServiceStarting(ServiceName name) { ServiceStarting(name.Host.ToString(), name.Service); }

        [Event(
            eventId: 8,
            Level = EventLevel.Informational,
            Task = Tasks.ServiceStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Started")]
        public void ServiceStarted(string hostName, string serviceName) { WriteEvent(8, hostName, serviceName); }
        [NonEvent]
        public void ServiceStarted(ServiceName name) { ServiceStarted(name.Host.ToString(), name.Service); }

        [Event(
            eventId: 9,
            Level = EventLevel.Critical,
            Task = Tasks.ServiceStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}/{1}: Failed to start with exception: {2}")]
        private void ServiceStartupFailed(string hostName, string serviceName, string exception) { WriteEvent(9, hostName, serviceName, exception); }
        [NonEvent]
        public void ServiceStartupFailed(ServiceName name, Exception ex) { ServiceStartupFailed(name.Host.ToString(), name.Service, ex.ToString()); }

        [Event(
            eventId: 10,
            Level = EventLevel.Critical,
            Message = "{0}: Missing HTTP Endpoints. 'http' and/or 'https' must be provided to run HTTP services.")]
        private void MissingHttpEndpoints(string hostName) { WriteEvent(10, hostName); }
        [NonEvent]
        public void MissingHttpEndpoints(ServiceHostName name) { MissingHttpEndpoints(name.ToString()); }

        [Event(
            eventId: 11,
            Level = EventLevel.Informational,
            Task = Tasks.HttpStartup,
            Opcode = EventOpcode.Start,
            Message = "{0}: Starting HTTP Services. http {1}, https {2}")]
        private void StartingHttpServices(string hostName, string http, string https) { WriteEvent(11, hostName, http, https); }
        [NonEvent]
        public void StartingHttpServices(ServiceHostName name, IPEndPoint http, IPEndPoint https) { StartingHttpServices(name.ToString(), http == null ? "<disabled>" : ("on port " + http.Port.ToString()), https == null ? "<disabled>" : ("on port " + https.Port.ToString())); }

        [Event(
            eventId: 12,
            Level = EventLevel.Informational,
            Task = Tasks.HttpStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Started HTTP Services")]
        private void StartedHttpServices(string hostName) { WriteEvent(12, hostName); }
        [NonEvent]
        public void StartedHttpServices(ServiceHostName name) { StartedHttpServices(name.ToString()); }

        [Event(
            eventId: 13,
            Level = EventLevel.Critical,
            Task = Tasks.HttpStartup,
            Opcode = EventOpcode.Stop,
            Message = "{0}: Error Starting HTTP Services: {1}")]
        private void ErrorStartingHttpServices(string hostName, string exception) { WriteEvent(13, hostName, exception); }
        [NonEvent]
        public void ErrorStartingHttpServices(ServiceHostName name, Exception ex) { ErrorStartingHttpServices(name.ToString(), ex.ToString()); }

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
        private void ServiceRunning(string hostName, string serviceName) { }
        [NonEvent]
        public void ServiceRunning(ServiceName name) { ServiceRunning(name.Host.ToString(), name.Service); }

        [Event(
            eventId: 18,
            Level = EventLevel.Informational,
            Opcode = EventOpcode.Stop,
            Task = Tasks.ServiceExecution,
            Message = "{0}/{1}: Stopped")]
        private void ServiceStoppedRunning(string hostName, string serviceName) { }
        [NonEvent]
        public void ServiceStoppedRunning(ServiceName name) { ServiceStoppedRunning(name.Host.ToString(), name.Service); }

        [Event(
            eventId: 19,
            Level = EventLevel.Critical,
            Opcode = EventOpcode.Stop,
            Task = Tasks.ServiceExecution,
            Message = "{0}/{1}: Exception during execution: {2}")]
        private void ServiceException(string hostName, string serviceName, string exception) { WriteEvent(19, hostName, serviceName, exception); }
        [NonEvent]
        public void ServiceException(ServiceName name, Exception ex) { ServiceException(name.Host.ToString(), name.Service, ex.ToString()); }

        [Event(
            eventId: 20,
            Level = EventLevel.Error,
            Message = "Exeception during HTTP request to {0}: {1}")]
        private void HttpException(string uri, string exception) { WriteEvent(20, uri, exception); }
        [NonEvent]
        public void HttpException(string uri, Exception ex) { HttpException(uri, ex.ToString()); }

        [Event(
            eventId: 21,
            Level = EventLevel.Error,
            Message = "Exeception during API request to {0} ({1}, {2}): {3}")]
        private void ApiException(string uri, string controller, string action, string exception) { WriteEvent(21, uri, controller, action, exception); }
        [NonEvent]
        public void ApiException(string uri, string controller, string action, Exception ex) { ApiException(uri, controller, action, ex.ToString()); }

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

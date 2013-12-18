using System;
using System.Reactive.Linq;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Sinks;
using NuGet.Services.Storage;
using NuGet.Services.Configuration;
using Autofac;
using Autofac.Core;
using NuGet.Services.Models;
using NuGet.Services.Composition;

namespace NuGet.Services.ServiceModel
{
    public abstract class NuGetService : IDisposable
    {
        private const string TraceTableBaseName = "Trace";
        private long _lastHeartbeatTicks = 0;
        
        private SinkSubscription<WindowsAzureTableSink> _globalSinkSubscription;
        private ServiceInstanceEntry _instanceEntry;

        public string ServiceName { get; private set; }
        public ServiceHost Host { get; private set; }
        public ServiceInstanceName InstanceName { get; private set; }

        public StorageHub Storage { get; set; }
        public ConfigurationHub Configuration { get; set; }
        public ILifetimeScope Container { get; protected set; }

        public DateTimeOffset? LastHeartbeat
        {
            get { return _lastHeartbeatTicks == 0 ? (DateTimeOffset?)null : new DateTimeOffset(_lastHeartbeatTicks, TimeSpan.Zero); }
        }

        public string TempDirectory { get; protected set; }

        protected NuGetService(string serviceName, ServiceHost host)
        {
            ServiceName = serviceName;
            Host = host;

            TempDirectory = Path.Combine(Path.GetTempPath(), "NuGetServices", serviceName);

            // Assign a unique id to this service (it'll be global across this host, but that's OK)
            int id = host.AssignInstanceId();

            // Build an instance name
            InstanceName = new ServiceInstanceName(host.Description.ServiceHostName, serviceName, id);
        }

        public virtual async Task<bool> Start(ILifetimeScope scope, ServiceInstanceEntry instanceEntry)
        {
            Container = scope;
            _instanceEntry = instanceEntry;

            Storage = scope.Resolve<StorageHub>();
            Configuration = scope.Resolve<ConfigurationHub>();

            if (Host == null)
            {
                throw new InvalidOperationException(Strings.NuGetService_HostNotSet);
            }
            Host.ShutdownToken.Register(OnShutdown);

            StartTracing();

            var ret = await OnStart();
            return ret;
        }

        public virtual async Task Run()
        {
            ServiceInstanceName.SetCurrent(InstanceName);

            if (Host == null)
            {
                throw new InvalidOperationException(Strings.NuGetService_HostNotSet);
            }
            await OnRun();
        }

        public void Dispose()
        {
            if (_globalSinkSubscription != null)
            {
                _globalSinkSubscription.Dispose();
            }

            var dispContainer = Container as IDisposable;
            if (dispContainer != null)
            {
                dispContainer.Dispose();
            }

            ServiceInstanceName.FreeCurrent();
        }

        public virtual Task Heartbeat()
        {
            var beatTime = DateTimeOffset.UtcNow;
            Interlocked.Exchange(ref _lastHeartbeatTicks, beatTime.Ticks);
            
            // TODO: PERF: This part won't be able to be synchronous for long...
            _instanceEntry.LastHeartbeat = beatTime;
            return Storage.Primary.Tables.Table<ServiceInstanceEntry>().InsertOrReplace(_instanceEntry);
        }

        protected virtual Task<bool> OnStart() { return Task.FromResult(true); }
        protected virtual void OnShutdown() { }
        protected abstract Task OnRun();

        protected virtual IEnumerable<EventSource> GetTraceEventSources()
        {
            return Enumerable.Empty<EventSource>();
        }

        public virtual void RegisterComponents(ContainerBuilder builder)
        {
        }

        private void StartTracing()
        {
            // Set up worker logging
            var listener = new ObservableEventListener();
            var stream = listener.Where(_ => Equals(ServiceInstanceName.GetCurrent(), InstanceName));
            foreach (var source in GetTraceEventSources())
            {
                listener.EnableEvents(source, EventLevel.Informational);
            }
            listener.EnableEvents(SemanticLoggingEventSource.Log, EventLevel.Informational);
            listener.EnableEvents(ServicePlatformEventSource.Log, EventLevel.Informational);
            _globalSinkSubscription = stream.LogToWindowsAzureTable(
                InstanceName.ToString(),
                Storage.Primary.ConnectionString,
                tableAddress: Storage.Primary.Tables.GetTableFullName(ServiceName + TraceTableBaseName));
        }
    }
}

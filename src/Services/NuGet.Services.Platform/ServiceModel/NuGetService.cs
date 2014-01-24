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
using NuGet.Services.Http.Models;

namespace NuGet.Services.ServiceModel
{
    public abstract class NuGetService : IDisposable
    {
        private const string TraceTableBaseName = "Trace";
        private long _lastHeartbeatTicks = 0;
        
        public ServiceHost Host { get; private set; }
        public ServiceName Name { get; private set; }

        public StorageHub Storage { get; set; }
        public ConfigurationHub Configuration { get; set; }
        public ILifetimeScope Container { get; protected set; }

        public DateTimeOffset? LastHeartbeat
        {
            get { return _lastHeartbeatTicks == 0 ? (DateTimeOffset?)null : new DateTimeOffset(_lastHeartbeatTicks, TimeSpan.Zero); }
        }

        public string TempDirectory { get; protected set; }

        protected NuGetService(ServiceName name, ServiceHost host)
        {
            Host = host;
            Name = name;

            TempDirectory = Path.Combine(Path.GetTempPath(), "NuGetServices", name.Service);
        }

        public virtual async Task<bool> Start(ILifetimeScope scope)
        {
            Container = scope;
            
            Storage = scope.Resolve<StorageHub>();
            Configuration = scope.Resolve<ConfigurationHub>();

            if (Host == null)
            {
                throw new InvalidOperationException(Strings.NuGetService_HostNotSet);
            }
            Host.ShutdownToken.Register(OnShutdown);

            var ret = await OnStart();
            return ret;
        }

        public virtual async Task Run()
        {
            if (Host == null)
            {
                throw new InvalidOperationException(Strings.NuGetService_HostNotSet);
            }
            await OnRun();
        }

        public void Dispose()
        {
            Container.Dispose();
        }

        public virtual void Heartbeat()
        {
            var beatTime = DateTimeOffset.UtcNow;
            Interlocked.Exchange(ref _lastHeartbeatTicks, beatTime.Ticks);
        }

        protected virtual Task<bool> OnStart() { return Task.FromResult(true); }
        protected virtual void OnShutdown() { }
        protected abstract Task OnRun();

        /// <summary>
        /// Returns a service description object, which is a simple model that lists information about the service
        /// </summary>
        /// <returns></returns>
        public virtual Task<object> Describe() { return Task.FromResult<object>(null); }

        /// <summary>
        /// Returns the current status of the service.
        /// </summary>
        /// <returns></returns>
        public virtual Task<object> GetCurrentStatus() { return Task.FromResult<object>(null); }

        protected virtual IEnumerable<EventSource> GetTraceEventSources()
        {
            return Enumerable.Empty<EventSource>();
        }

        public virtual void RegisterComponents(ContainerBuilder builder)
        {
        }
    }
}

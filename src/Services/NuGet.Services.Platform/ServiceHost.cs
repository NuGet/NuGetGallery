using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using NuGet.Services.Composition;
using NuGet.Services.Configuration;
using NuGet.Services.Storage;

namespace NuGet.Services
{
    public abstract class ServiceHost
    {
        private CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();
        private IContainer _container;
        private AutofacComponentContainer _containerWrapper;

        public abstract string Name { get; }
        public CancellationToken ShutdownToken { get { return _shutdownTokenSource.Token; } }

        public IReadOnlyList<NuGetService> Services { get; private set; }

        public IComponentContainer Container { get { return _containerWrapper; } }

        /// <summary>
        /// Starts all services in the host and blocks until they have completed starting.
        /// </summary>
        public bool StartAndWait()
        {
            return Start().Result;
        }

        /// <summary>
        /// Starts all services, returning a task that will complete when they have completed starting
        /// </summary>
        public async Task<bool> Start()
        {
            return (await Task.WhenAll(s => s.Start())).All(b => b);
        }

        /// <summary>
        /// Runs all services, returning a task that will complete when they stop
        /// </summary>
        public Task Run()
        {
            return Task.WhenAll(Services.Select(s => s.Run()));
        }

        /// <summary>
        /// Requests that all services shut down. Calling this will cause the task returned by Run to complete (eventually)
        /// </summary>
        public void Shutdown()
        {
            ServicePlatformEventSource.Log.Stopping(Name);
            _shutdownTokenSource.Cancel();
        }

        public virtual IPEndPoint GetEndpoint(string name)
        {
            throw new NotSupportedException(Strings.ServiceHost_DoesNotSupportEndpoints);
        }

        public virtual string GetConfigurationSetting(string fullName)
        {
            return ConfigurationManager.AppSettings[fullName];
        }

        public virtual void Initialize(Action<IServiceRegistrar> registrations)
        {
            ContainerBuilder builder = new ContainerBuilder();

            // Add modules
            foreach (var module in Enumerable.Concat(GetCoreModules(), GetModules()))
            {
                builder.RegisterModule(module);
            }

            // Register services
            registrations(new AutofacServiceRegistrar(builder));
            
            _container = builder.Build();
            _containerWrapper = new AutofacComponentContainer(_container);

            // Now get the services
            Services = _container.Resolve<IReadOnlyList<NuGetService>>();
        }

        /// <summary>
        /// Should only be overridden if you know what you're doing!
        /// </summary>
        protected virtual IEnumerable<Module> GetCoreModules()
        {
            yield return new NuGetCoreModule(this);
        }

        protected virtual IEnumerable<Module> GetModules()
        {
            yield break;
        }
    }
}

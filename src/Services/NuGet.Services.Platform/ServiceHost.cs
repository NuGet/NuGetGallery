using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using NuGet.Services.Configuration;
using NuGet.Services.Storage;

namespace NuGet.Services
{
    public abstract class ServiceHost
    {
        private CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();
        private IContainer _container;
        private IReadOnlyList<Type> _serviceTypes;
        
        public abstract string Name { get; }
        public CancellationToken ShutdownToken { get { return _shutdownTokenSource.Token; } }

        public IReadOnlyList<NuGetService> Instances { get; private set; }

        public IContainer Container { get { return _container; } }

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
            var instances = await Task.WhenAll(_serviceTypes.Select(StartService));
            Instances = instances.Where(s => s != null);
            return instances.All(s => s != null);
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

        public virtual void Initialize()
        {
            ContainerBuilder builder = new ContainerBuilder();

            // Add modules
            builder.RegisterModule(new NuGetCoreModule(this));
            
            _container = builder.Build();

            // Now get the services
            _serviceTypes = GetServices().ToList().AsReadOnly();

            var invalidService = _serviceTypes.FirstOrDefault(t => !typeof(NuGetService).IsAssignableFrom);
            if (invalidService != null)
            {
                throw new InvalidCastException(String.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ServiceHost_NotAValidServiceType,
                    invalidService.FullName));
            }
        }

        private Task<NuGetService> StartService(Type service)
        {
            // Create a lifetime scope
            var scope = _container.BeginLifetimeScope();
            var service = (NuGetService)scope.Resolve(service);
            var builder = new ContainerBuilder();
            builder.RegisterInstance(service).As<NuGetService>();
            service.RegisterComponents(builder);
            builder.Update(scope.ComponentRegistry);

            return service.Start(scope);
        }

        protected abstract IEnumerable<Type> GetServices();
    }
}

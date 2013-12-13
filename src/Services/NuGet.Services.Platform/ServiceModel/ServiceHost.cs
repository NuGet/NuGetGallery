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
using Autofac.Features.ResolveAnything;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using NuGet.Services.Configuration;
using NuGet.Services.Models;
using NuGet.Services.Storage;

namespace NuGet.Services.ServiceModel
{
    public abstract class ServiceHost
    {
        private CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();
        private IContainer _container;
        private IReadOnlyList<Type> _serviceTypes;

        private ConfigurationHub _config;
        private StorageHub _storage;
        private volatile int _nextId = 0;

        public abstract ServiceHostDescription Description { get; }

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
            Instances = instances.Where(s => s != null).ToList().AsReadOnly();
            return instances.All(s => s != null);
        }

        /// <summary>
        /// Runs all services, returning a task that will complete when they stop
        /// </summary>
        public Task Run()
        {
            return Task.WhenAll(Instances.Select(s => s.Run()));
        }

        /// <summary>
        /// Requests that all services shut down. Calling this will cause the task returned by Run to complete (eventually)
        /// </summary>
        public void Shutdown()
        {
            ServicePlatformEventSource.Log.HostShutdownRequested(Description.ServiceHostName.ToString());
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

        public virtual async Task Initialize()
        {
            // Initialize the very very basic platform logging system (just logs service platform events to a single host-specific log file)
            // This way, if the below code fails, we can see some kind of log as to why.
            InitializePlatformLogging();

            ServicePlatformEventSource.Log.HostStarting(Description.ServiceHostName.ToString());
            try
            {
                ContainerBuilder builder = new ContainerBuilder();

                // Resolve a concrete type that isn't overridden by just constructing it
                builder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource());

                // Add core module containing most of our components
                builder.RegisterModule(new NuGetCoreModule(this));

                _container = builder.Build();

                // Manually resolve components the host uses
                _config = _container.Resolve<ConfigurationHub>();
                _storage = _container.Resolve<StorageHub>();

                // Now get the services
                _serviceTypes = GetServices().ToList().AsReadOnly();

                var invalidService = _serviceTypes.FirstOrDefault(t => !typeof(NuGetService).IsAssignableFrom(t));
                if (invalidService != null)
                {
                    throw new InvalidCastException(String.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ServiceHost_NotAValidServiceType,
                        invalidService.FullName));
                }

                // Report status
                var entry = new ServiceHostEntry(Description);
                await _storage.Primary.Tables.Table<ServiceHostEntry>().InsertOrReplace(entry);
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.HostStartupFailed(Description.ServiceHostName.ToString(), ex);
                throw; // Don't stop the exception, we have to abort the startup process
            }
            ServicePlatformEventSource.Log.HostStarted(Description.ServiceHostName.ToString());
        }

        public int AssignInstanceId()
        {
            return Interlocked.Increment(ref _nextId) - 1;
        }

        protected abstract void InitializePlatformLogging();
        protected abstract IEnumerable<Type> GetServices();

        private async Task<NuGetService> StartService(Type serviceType)
        {
            // Initialize the serice, create the necessary IoC components and construct the instance.
            ServicePlatformEventSource.Log.ServiceInitializing(Description.ServiceHostName.ToString(), serviceType);
            NuGetService service = null;
            ILifetimeScope scope = null;
            try
            {
                // Create a lifetime scope and register the service in it
                scope = _container.BeginLifetimeScope(b =>
                {
                    b.RegisterType(serviceType)
                     .As<NuGetService>()
                     .As(serviceType)
                     .SingleInstance();
                });

                // Resolve the instance
                service = (NuGetService)scope.Resolve(serviceType);
                
                // Augment the scope with service-specific services
                var builder = new ContainerBuilder();
                service.RegisterComponents(builder);
                builder.Update(scope.ComponentRegistry);
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.ServiceInitializationFailed(Description.ServiceHostName.ToString(), ex, serviceType);
                throw;
            }

            // Because of the "throw" in the catch block, we won't arrive here unless successful
            ServicePlatformEventSource.Log.ServiceInitialized(Description.ServiceHostName.ToString(), serviceType);

            // Report that we're starting the service
            var entry = new ServiceInstanceEntry(service.InstanceName, AssemblyInformation.FromAssembly(service.GetType().Assembly));
            await _storage.Primary.Tables.Table<ServiceInstanceEntry>().InsertOrReplace(entry);
            
            // Start the service and return it if the start succeeds.
            ServicePlatformEventSource.Log.ServiceStarting(Description.ServiceHostName.ToString(), service.InstanceName.ToString());
            bool result = false;
            try
            {
                result = await service.Start(scope, entry);
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.ServiceStartupFailed(Description.ServiceHostName.ToString(), service.InstanceName.ToString(), ex);
                throw;
            }

            // Update the status entry
            entry.StartedAt = entry.LastHeartbeat = DateTimeOffset.UtcNow;
            await _storage.Primary.Tables.Table<ServiceInstanceEntry>().InsertOrReplace(entry);
            
            // Because of the "throw" in the catch block, we won't arrive here unless successful
            ServicePlatformEventSource.Log.ServiceStarted(Description.ServiceHostName.ToString(), service.InstanceName.ToString());

            if (result)
            {
                return service;
            }
            return null;
        }
    }
}

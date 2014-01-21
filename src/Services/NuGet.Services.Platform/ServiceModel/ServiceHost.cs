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
using NuGet.Services.Composition;
using NuGet.Services.Configuration;
using NuGet.Services.Models;
using NuGet.Services.Storage;

namespace NuGet.Services.ServiceModel
{
    public abstract class ServiceHost
    {
        private CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();
        private IContainer _container;
        private IComponentContainer _containerWrapper;
        private AssemblyInformation _runtimeInformation = typeof(ServiceHost).GetAssemblyInfo();

        private volatile int _nextId = 0;

        public abstract ServiceHostDescription Description { get; }

        public CancellationToken ShutdownToken { get { return _shutdownTokenSource.Token; } }

        public StorageHub Storage { get; private set; }
        public ConfigurationHub Config { get; private set; }
        public IReadOnlyList<NuGetService> Instances { get; private set; }

        public AssemblyInformation RuntimeInformation { get { return _runtimeInformation; } }

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
            var instances = await Task.WhenAll(Instances.Select(StartService));
            Instances = instances.Where(s => s != null).ToList().AsReadOnly();
            return instances.All(s => s != null);
        }

        /// <summary>
        /// Runs all services, returning a task that will complete when they stop
        /// </summary>
        public Task Run()
        {
            return Task.WhenAll(Instances.Select(RunService));
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
            return null;
        }

        public virtual string GetConfigurationSetting(string fullName)
        {
            return ConfigurationManager.AppSettings[fullName];
        }
        
        public virtual async Task Initialize()
        {
            // Initialize the very very basic platform logging system (just logs service platform events to a single host-specific log file)
            // This way, if the below code fails, we can see some kind of log as to why.
            InitializeLocalLogging();

            ServicePlatformEventSource.Log.HostStarting(Description.ServiceHostName.ToString());
            try
            {
                // Build the container
                _container = Compose();
                _containerWrapper = new AutofacComponentContainer(_container);

                // Manually resolve components the host uses
                Storage = _container.Resolve<StorageHub>();
                Config = _container.Resolve<ConfigurationHub>();

                // Now get the services
                var list = GetServices().ToList();
                var management = GetManagementService();
                if (management != null)
                {
                    list.Add(management);
                }
                Instances = list.AsReadOnly();

                // Report status
                await ReportHostInitialized();

                // Start full cloud logging
                InitializeCloudLogging();
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
            // It's OK to pass volatile fields as ref to Interlocked APIs
            //  "...there are exceptions to this, such as when calling an interlocked API"
            //  from http://msdn.microsoft.com/en-us/library/4bw5ewxy.aspx
#pragma warning disable 0420 
            return Interlocked.Increment(ref _nextId) - 1;
#pragma warning restore 0420
        }

        protected virtual IContainer Compose()
        {
            ContainerBuilder builder = new ContainerBuilder();

            // Resolve a concrete type that isn't overridden by just constructing it
            builder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource());

            // Add core module containing most of our components
            builder.RegisterModule(new NuGetCoreModule(this));

            return builder.Build();
        }

        protected virtual async Task ReportHostInitialized()
        {
            var entry = new ServiceHostEntry(Description);
            
            // Get the http-instance endpoint if it exists
            var instanceEp = GetEndpoint(Constants.HttpInstanceEndpoint);
            if (instanceEp != null)
            {
                entry.InstancePort = instanceEp.Port;
            }

            if (Storage != null && Storage.Primary != null)
            {
                await Storage.Primary.Tables.Table<ServiceHostEntry>().InsertOrReplace(entry);
            }
        }

        /// <summary>
        /// Initializes low-level logging of data to the local machine
        /// </summary>
        protected abstract void InitializeLocalLogging();
        /// <summary>
        /// Initializes logging of data to external sources in order to gather debugging data in the cloud
        /// </summary>
        protected abstract void InitializeCloudLogging();
        /// <summary>
        /// Gets instances of the services to host
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerable<NuGetService> GetServices();
        /// <summary>
        /// Gets the instance of the HTTP management service for this host
        /// </summary>
        /// <returns></returns>
        protected abstract NuGetService GetManagementService();

        private async Task RunService(NuGetService service)
        {
            ServicePlatformEventSource.Log.ServiceRunning(service.InstanceName);
            try
            {
                await service.Run();
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.ServiceException(service.InstanceName, ex);
                throw;
            }
            ServicePlatformEventSource.Log.ServiceStoppedRunning(service.InstanceName);
        }

        internal async Task<NuGetService> StartService(NuGetService service)
        {
            // Initialize the serice, create the necessary IoC components and construct the instance.
            ServicePlatformEventSource.Log.ServiceInitializing(service.InstanceName);
            ILifetimeScope scope = null;
            try
            {
                // Construct a scope with the instance in it
                scope = _container.BeginLifetimeScope(builder =>
                {
                    builder.RegisterInstance(service)
                     .As<NuGetService>()
                     .As(service.GetType());
                    builder.Register(c => c.Resolve<NuGetService>().InstanceName)
                        .As<ServiceInstanceName>();

                    // Add the container itself to the container
                    builder.Register(c => scope)
                        .As<ILifetimeScope>()
                        .SingleInstance();
                    builder.Register(c => new AutofacComponentContainer(c.Resolve<ILifetimeScope>()))
                        .As<IComponentContainer>()
                        .As<IServiceProvider>()
                        .SingleInstance();

                    // Add components provided by the service
                    service.RegisterComponents(builder);
                });
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.ServiceInitializationFailed(service.InstanceName, ex);
                throw;
            }

            // Because of the "throw" in the catch block, we won't arrive here unless successful
            ServicePlatformEventSource.Log.ServiceInitialized(service.InstanceName);

            // Report that we're starting the service
            var entry = new ServiceInstanceEntry(service.InstanceName, service.GetType().GetAssemblyInfo());
            await UpdateServiceInstanceEntry(entry);
            
            // Start the service and return it if the start succeeds.
            ServicePlatformEventSource.Log.ServiceStarting(service.InstanceName);
            bool result = false;
            try
            {
                result = await service.Start(scope, entry);
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.ServiceStartupFailed(service.InstanceName, ex);
                throw;
            }

            // Update the status entry
            entry.StartedAt = entry.LastHeartbeat = DateTimeOffset.UtcNow;
            await UpdateServiceInstanceEntry(entry);
            
            // Because of the "throw" in the catch block, we won't arrive here unless successful
            ServicePlatformEventSource.Log.ServiceStarted(service.InstanceName);

            if (result)
            {
                return service;
            }
            return null;
        }

        private async Task UpdateServiceInstanceEntry(ServiceInstanceEntry entry)
        {
            if (Storage != null && Storage.Primary != null)
            {
                await Storage.Primary.Tables.Table<ServiceInstanceEntry>().InsertOrReplace(entry);
            }
        }
    }
}

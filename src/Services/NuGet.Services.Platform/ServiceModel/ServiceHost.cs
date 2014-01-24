using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Features.ResolveAnything;
using Owin;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using NuGet.Services.Configuration;
using NuGet.Services.Http;
using NuGet.Services.Models;
using NuGet.Services.Storage;
using NuGet.Services.Http.Middleware;
using NuGet.Services.Http.Authentication;
using NuGet.Services.Http.Models;

namespace NuGet.Services.ServiceModel
{
    public abstract class ServiceHost
    {
        private CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();
        private IContainer _container;
        private AssemblyInformation _runtimeInformation = typeof(ServiceHost).GetAssemblyInfo();
        private IDisposable _httpServerLifetime;

        private volatile int _nextId = 0;

        public abstract ServiceHostDescription Description { get; }

        public CancellationToken ShutdownToken { get { return _shutdownTokenSource.Token; } }

        public StorageHub Storage { get; private set; }
        public ConfigurationHub Config { get; private set; }
        public IReadOnlyDictionary<string, ServiceDefinition> Services { get; private set; }
        public IReadOnlyList<NuGetService> Instances { get; private set; }
        public IReadOnlyList<NuGetHttpService> HttpServiceInstances { get; private set; }
        
        private IReadOnlyDictionary<Type, NuGetService> InstancesByType { get; set; }
        private IReadOnlyDictionary<string, NuGetService> InstancesByName { get; set; }

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
            var instances = await Task.WhenAll(Services.Values.Select(StartService));
            HttpServiceInstances = instances.OfType<NuGetHttpService>().ToList().AsReadOnly();
            StartHttp(HttpServiceInstances);

            Instances = instances.Where(s => s != null).ToList().AsReadOnly();
            InstancesByType = new ReadOnlyDictionary<Type, NuGetService>(Instances.ToDictionary(s => s.GetType()));
            InstancesByName = new ReadOnlyDictionary<string, NuGetService>(Instances.ToDictionary(s => s.Name.Service, StringComparer.OrdinalIgnoreCase));

            return instances.All(s => s != null);
        }

        /// <summary>
        /// Runs all services, returning a task that will complete when they stop
        /// </summary>
        public async Task Run()
        {
            await Task.WhenAll(Instances.Select(RunService));
            foreach (var instance in Instances)
            {
                instance.Dispose();
            }
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

        public virtual NuGetService GetInstance(string name)
        {
            NuGetService ret;
            if (InstancesByName == null || !InstancesByName.TryGetValue(name, out ret))
            {
                return null;
            }
            return ret; 
        }

        public virtual T GetInstance<T>() where T : NuGetService
        {
            NuGetService ret;
            if (InstancesByType == null || !InstancesByType.TryGetValue(typeof(T), out ret))
            {
                return default(T);
            }
            return (T)ret;
        }
        
        public virtual void Initialize()
        {
            // Initialize the very very basic platform logging system (just logs service platform events to a single host-specific log file)
            // This way, if the below code fails, we can see some kind of log as to why.
            InitializeLocalLogging();

            ServicePlatformEventSource.Log.HostStarting(Description.ServiceHostName.ToString());
            try
            {
                // Load the services
                var dict = GetServices().ToDictionary(s => s.Name);
                Services = new ReadOnlyDictionary<string, ServiceDefinition>(dict);

                // Build the container
                _container = Compose();
                
                // Manually resolve components the host uses
                Storage = _container.Resolve<StorageHub>();
                Config = _container.Resolve<ConfigurationHub>();

                // Report status
                ReportHostInitialized();

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

        private void StartHttp(IEnumerable<NuGetHttpService> httpServices)
        {
            var httpEndpoint = GetEndpoint("http");
            var httpsEndpoint = GetEndpoint("https");

            // Set up start options
            var options = new StartOptions();
            var httpConfig = Config.GetSection<HttpConfiguration>();
            if (httpEndpoint != null)
            {
                options.Urls.Add("http://+:" + httpEndpoint.Port.ToString() + "/" + httpConfig.BasePath);
                options.Urls.Add("http://localhost:" + httpEndpoint.Port.ToString() + "/" + httpConfig.BasePath);
            }
            if (httpsEndpoint != null)
            {
                options.Urls.Add("https://+:" + httpsEndpoint.Port.ToString() + "/" + httpConfig.BasePath);
                options.Urls.Add("https://localhost:" + httpsEndpoint.Port.ToString() + "/" + httpConfig.BasePath);
            }
            if (options.Urls.Count == 0)
            {
                ServicePlatformEventSource.Log.MissingHttpEndpoints(Description.ServiceHostName);
            }
            else
            {
                ServicePlatformEventSource.Log.StartingHttpServices(Description.ServiceHostName, httpEndpoint, httpsEndpoint);
                try
                {
                    _httpServerLifetime = WebApp.Start(options, app => ConfigureHttp(httpServices, app));
                }
                catch (Exception ex)
                {
                    ServicePlatformEventSource.Log.ErrorStartingHttpServices(Description.ServiceHostName, ex);
                    throw;
                }
                ServicePlatformEventSource.Log.StartedHttpServices(Description.ServiceHostName);
            }
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
            ContainerBuilder builder = CreateContainerBuilder();

            // Add core module containing most of our components
            builder.RegisterModule(new NuGetCoreModule(this));

            // Add Services
            foreach (var service in Services)
            {
                builder
                    .RegisterType(service.Value.Type)
                    .Named<NuGetService>(service.Key)
                    .SingleInstance();
            }

            return builder.Build();
        }

        private void ConfigureHttp(IEnumerable<NuGetHttpService> httpServices, IAppBuilder app)
        {
            // Add common host middleware in at the beginning of the pipeline
            var config = Config.GetSection<HttpConfiguration>();
            if (!String.IsNullOrEmpty(config.AdminKey))
            {
                app.UseAdminKeyAuthentication(new AdminKeyAuthenticationOptions()
                {
                    Key = config.AdminKey,
                    GrantedRole = Roles.Admin
                });
            }

            // Add the service information middleware, which handles root requests and "/_info" requests
            app.UseNuGetServiceInformation(this);

            // Map the HTTP-compatible services to their respective roots
            foreach (var service in httpServices)
            {
                app.Map(service.BasePath, service.StartHttp);
            }
        }

        protected virtual ContainerBuilder CreateContainerBuilder()
        {
            return new ContainerBuilder();
        }

        protected virtual void ReportHostInitialized()
        {
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
        protected abstract IEnumerable<ServiceDefinition> GetServices();

        private async Task RunService(NuGetService service)
        {
            ServicePlatformEventSource.Log.ServiceRunning(service.Name);
            try
            {
                await service.Run();
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.ServiceException(service.Name, ex);
                throw;
            }
            ServicePlatformEventSource.Log.ServiceStoppedRunning(service.Name);
        }

        internal async Task<NuGetService> StartService(ServiceDefinition service)
        {
            // Create a full service name
            var name = new ServiceName(Description.ServiceHostName, service.Name);

            // Initialize the serice, create the necessary IoC components and construct the instance.
            ServicePlatformEventSource.Log.ServiceInitializing(name);
            ILifetimeScope scope = null;
            NuGetService instance;
            try
            {
                // Resolve the service
                instance = _container.ResolveNamed<NuGetService>(
                    service.Name, 
                    new NamedParameter("name", name));

                // Construct a scope with the service
                scope = _container.BeginLifetimeScope(builder =>
                {
                    builder.RegisterInstance(instance)
                     .As<NuGetService>()
                     .As(service.Type);
                    builder.RegisterInstance(name)
                        .As<ServiceName>();

                    // Add the container itself to the container
                    builder.Register(c => scope)
                        .As<ILifetimeScope>()
                        .SingleInstance();

                    // Add components provided by the service
                    instance.RegisterComponents(builder);
                });
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.ServiceInitializationFailed(name, ex);
                throw;
            }

            // Because of the "throw" in the catch block, we won't arrive here unless successful
            ServicePlatformEventSource.Log.ServiceInitialized(name);

            // Start the service and return it if the start succeeds.
            ServicePlatformEventSource.Log.ServiceStarting(name);
            bool result = false;
            try
            {
                result = await instance.Start(scope);
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.ServiceStartupFailed(name, ex);
                throw;
            }

            // Because of the "throw" in the catch block, we won't arrive here unless successful
            ServicePlatformEventSource.Log.ServiceStarted(name);

            if (result)
            {
                return instance;
            }
            return null;
        }
    }
}

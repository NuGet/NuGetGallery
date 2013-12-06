using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Autofac;
using NuGet.Services.Composition;
using NuGetGallery.Storage;

namespace NuGet.Services
{
    public abstract class ServiceHost
    {
        private CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();
        private List<NuGetService> _services = new List<NuGetService>();
        private IContainer _container;

        public abstract string Name { get; }
        public abstract ServiceConfiguration Configuration { get; }
        public CancellationToken ShutdownToken { get { return _shutdownTokenSource.Token; } }

        public IReadOnlyList<NuGetService> Services { get { return _services.AsReadOnly(); } }

        public void Shutdown()
        {
            ServicePlatformEventSource.Log.Finished(Name);
            ServicePlatformEventSource.Log.Stopping(Name);
            _shutdownTokenSource.Cancel();
        }

        public virtual IPEndPoint GetEndpoint(string name)
        {
            // The following is a super cheap way of leaving a note I can't ignore :)
            Change NuGetService to be the following:
            // a) A factory that creates ServiceInstances (which do the start/stop thing)
            // b) Something that registers services in the container?
            return null;
        }

        public virtual void AttachService(NuGetService service)
        {
            _services.Add(service);

            // Add the service to the container
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterInstance<NuGetService>(service);
            builder.Update(_container);
        }

        protected virtual IServiceContainer CreateContainer()
        {
            ContainerBuilder builder = new ContainerBuilder();

            // Add core services
            builder.RegisterInstance<ServiceHost>(this);
            builder.RegisterInstance<ServiceConfiguration>(Configuration);
            builder.RegisterInstance<StorageHub>(Configuration.Storage);

            // Let subclasses add their own services
            AddServices(builder);

            _container = builder.Build();
            return _container;
        }

        protected virtual void AddServices(ContainerBuilder builder)
        {
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Autofac.Features.ResolveAnything;
using NuGet.Services.Composition;
using NuGet.Services.Configuration;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;

namespace NuGet.Services.TestInfrastructure
{
    public class TestServiceHost : ServiceHost
    {
        private Action<ContainerBuilder> _componentRegistrations;

        public IComponentContainer Container { get; private set; }

        public TestServiceHost() : this(null) { }
        public TestServiceHost(Action<ContainerBuilder> componentRegistrations)
        {
            _componentRegistrations = componentRegistrations;
        }

        public override ServiceHostDescription Description
        {
            get
            {
                return new ServiceHostDescription(
                    new ServiceHostName(
                        new DatacenterName(
                            "local",
                            42),
                        "testhost"),
                    "testmachine");
            }
        }

        protected override void InitializeLocalLogging()
        {
        }

        protected override IEnumerable<NuGetService> GetServices()
        {
            return Enumerable.Empty<NuGetService>();
        }

        protected override IContainer Compose()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource());
            builder.RegisterInstance(this).As<ServiceHost>();
            builder.RegisterType<ConfigurationHub>().UsingConstructor(typeof(ServiceHost));
            builder.RegisterType<StorageHub>().UsingConstructor(typeof(ConfigurationHub));

            if (_componentRegistrations != null)
            {
                _componentRegistrations(builder);
            }

            var container = builder.Build();
            Container = new AutofacComponentContainer(container);
            return container;
        }

        protected override Task ReportHostInitialized()
        {
            // Don't have storage or monitoring in tests.
            return Task.FromResult<object>(null);
        }
    }
}

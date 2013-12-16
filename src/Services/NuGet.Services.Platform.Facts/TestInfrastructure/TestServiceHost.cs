using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Autofac.Features.ResolveAnything;
using NuGet.Services.Configuration;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;

namespace NuGet.Services.TestInfrastructure
{
    public class TestServiceHost : ServiceHost
    {
        private IEnumerable<Type> _services;
        private Action<ContainerBuilder> _componentRegistrations;

        public TestServiceHost() : this(Enumerable.Empty<Type>()) { }
        public TestServiceHost(IEnumerable<Type> services) : this(services, null) { }
        public TestServiceHost(IEnumerable<Type> services, Action<ContainerBuilder> componentRegistrations)
        {
            _services = services;
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

        protected override void InitializePlatformLogging()
        {
        }

        protected override IEnumerable<Type> GetServices()
        {
            return _services;
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

            return builder.Build();
        }

        protected override Task ReportHostInitialized()
        {
            // Don't have storage or monitoring in tests.
            return Task.FromResult<object>(null);
        }
    }
}

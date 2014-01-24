using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using NuGet.Services.Configuration;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;

namespace NuGet.Services.TestInfrastructure
{
    public class TestServiceHost : ServiceHost
    {
        private Action<ContainerBuilder> _componentRegistrations;

        public IContainer Container { get; private set; }

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
                        "testhost",
                        0),
                    "testmachine");
            }
        }

        protected override void InitializeLocalLogging()
        {
        }

        protected override IEnumerable<ServiceDefinition> GetServices()
        {
            return Enumerable.Empty<ServiceDefinition>();
        }

        protected override IContainer Compose()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterInstance(this).As<ServiceHost>();
            builder.RegisterType<ConfigurationHub>().UsingConstructor(typeof(ServiceHost));
            builder.RegisterType<StorageHub>().UsingConstructor(typeof(ConfigurationHub));

            if (_componentRegistrations != null)
            {
                _componentRegistrations(builder);
            }

            Container = builder.Build();
            return Container;
        }

        protected override void ReportHostInitialized()
        {
        }

        protected override void InitializeCloudLogging()
        {
        }
    }
}

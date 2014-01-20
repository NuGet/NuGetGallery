using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autofac;
using NuGet.Services.Configuration;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;

namespace NuGet.Services
{
    internal class NuGetCoreModule : Module
    {
        private ServiceHost _serviceHost;

        public NuGetCoreModule(ServiceHost serviceHost)
        {
            _serviceHost = serviceHost;
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterInstance<ServiceHost>(_serviceHost);
            builder.RegisterType<ConfigurationHub>()
                .UsingConstructor(typeof(ServiceHost))
                .SingleInstance();
            builder.RegisterType<StorageHub>()
                .UsingConstructor(typeof(ConfigurationHub))
                .SingleInstance();
            builder.RegisterInstance(Clock.RealClock).As<Clock>();
        }
    }
}

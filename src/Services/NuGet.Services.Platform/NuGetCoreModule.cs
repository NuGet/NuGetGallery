using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autofac;
using NuGet.Services.Configuration;

namespace NuGet.Services
{
    public class NuGetCoreModule : Module
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
            builder.RegisterType<ConfigurationHub>();
        }
    }
}

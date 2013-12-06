using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Autofac;
using Autofac.Builder;

namespace NuGet.Services.Composition
{
    public class AutofacServiceRegistrar : IServiceRegistrar
    {
        private ContainerBuilder _builder;

        public AutofacServiceRegistrar(ContainerBuilder builder)
        {
            _builder = builder;
        }

        public void RegisterService(Type nugetServiceType)
        {
            Debug.Assert(typeof(NuGetService).IsAssignableFrom(nugetServiceType), "RegisterService must be given a type that inherits from NuGetService");
            ApplyRegistrationConfiguration(_builder.RegisterType(nugetServiceType));
        }

        public void RegisterInstance(NuGetService instance)
        {
            ApplyRegistrationConfiguration(_builder.RegisterInstance(instance));
        }

        private void ApplyRegistrationConfiguration<T>(IRegistrationBuilder<T, IConcreteActivatorData, SingleRegistrationStyle> registration)
        {
            registration
                .As<NuGetService>()
                .SingleInstance() // Services are singletons. Well kinda, there can be multiple instances if the caller calls this multiple times. But each "instance" is a singleton :)
                .PropertiesAutowired(options: PropertyWiringOptions.PreserveSetValues); // Property injection ==> No giant constructors for subclasses of NuGetService to replicate
        }
    }
}

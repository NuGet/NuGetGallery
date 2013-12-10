using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac.Builder;
using NuGet.Services;

namespace Autofac
{
    public static class AutofacExtensions
    {
        public static IRegistrationBuilder<TImplementation, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterNuGetService<TImplementation>(this ContainerBuilder builder)
            where TImplementation : NuGetService
        {
            return builder.RegisterType<TImplementation>()
                .As<NuGetService>()
                .SingleInstance();
        }
    }
}

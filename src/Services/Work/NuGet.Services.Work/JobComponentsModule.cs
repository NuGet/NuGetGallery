using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using NuGet.Services.Configuration;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.Jobs
{
    public class JobComponentsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<InvocationQueue>().AsSelf().UsingConstructor(typeof(Clock), typeof(ServiceInstanceName), typeof(ConfigurationHub));
        }
    }
}

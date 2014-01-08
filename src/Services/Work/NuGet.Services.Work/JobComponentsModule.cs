using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using NuGet.Services.Configuration;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;

namespace NuGet.Services.Work
{
    public class JobComponentsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<InvocationQueue>().AsSelf().UsingConstructor(
                typeof(Clock), 
                typeof(ServiceInstanceName), 
                typeof(StorageHub),
                typeof(ConfigurationHub));
        }
    }
}
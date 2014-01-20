using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using NuGet.Services.Configuration;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;
using NuGet.Services.Work.Monitoring;

namespace NuGet.Services.Work
{
    public class JobComponentsModule : Module
    {
        private InvocationQueue _queue;
        
        public JobComponentsModule() : this(null) { }
        public JobComponentsModule(InvocationQueue queue)
        {
            _queue = queue;
        }

        protected override void Load(ContainerBuilder builder)
        {
            if (_queue != null)
            {
                builder.RegisterInstance(_queue).As<InvocationQueue>();
            }
            else
            {
                builder.RegisterType<InvocationQueue>().AsSelf().UsingConstructor(
                    typeof(Clock),
                    typeof(ServiceInstanceName),
                    typeof(StorageHub),
                    typeof(ConfigurationHub));
            }
        }
    }
}
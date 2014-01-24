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
        private string _instanceName;
        
        public JobComponentsModule(string instanceName) : this(instanceName, null) { }
        public JobComponentsModule(string instanceName, InvocationQueue queue)
        {
            _instanceName = instanceName;
            _queue = queue;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<JobRunner>()
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<JobDispatcher>()
                .AsSelf()
                .SingleInstance();

            if (_queue != null)
            {
                builder.RegisterInstance(_queue).As<InvocationQueue>();
            }
            else
            {
                builder
                    .RegisterType<InvocationQueue>()
                    .AsSelf()
                    .UsingConstructor(
                        typeof(Clock),
                        typeof(string),
                        typeof(StorageHub),
                        typeof(ConfigurationHub))
                    .WithParameter(
                        new NamedParameter("instanceName", _instanceName));
            }
        }
    }
}
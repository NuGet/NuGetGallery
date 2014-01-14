using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using NuGet.Services.Configuration;
using NuGet.Services.ServiceModel;
using NuGet.Services.Storage;
using NuGet.Services.Work.Infrastructure;
using NuGet.Services.Work.Monitoring;

namespace NuGet.Services.Work
{
    public class JobComponentsModule : Module
    {
        private InvocationQueue _queue;
        private InvocationLogCaptureFactory _captureFactory;

        public JobComponentsModule() : this(null, null) { }
        public JobComponentsModule(InvocationQueue queue, InvocationLogCaptureFactory captureFactory)
        {
            _queue = queue;
            _captureFactory = captureFactory;
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

            if (_captureFactory != null)
            {
                builder.RegisterInstance(_captureFactory).As<InvocationLogCaptureFactory>();
            }
            else
            {
                builder.RegisterType<BlobInvocationLogCaptureFactory>().As<InvocationLogCaptureFactory>();
            }
        }
    }
}
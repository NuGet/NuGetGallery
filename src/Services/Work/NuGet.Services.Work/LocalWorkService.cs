using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.Work
{
    [Service("Work")]
    public class LocalWorkService : WorkService
    {
        [Obsolete("Do not use, use LocalWorkService.Create instead")]
        public LocalWorkService(ServiceName name, ServiceHost host)
            : base(name, host)
        {
            Queue = InvocationQueue.Null;
        }

        public static Task<WorkService> Create()
        {
            return Create(new Dictionary<string, string>());
        }

        public static async Task<WorkService> Create(IDictionary<string, string> configuration)
        {
            var host = new LocalServiceHost(
                new ServiceHostName(
                    new DatacenterName("local", 0),
                    "work",
                    0),
                configuration);
            var name = new ServiceName(host.Description.ServiceHostName, ServiceDefinition.FromType<WorkService>().Name);
            host.LocalServices.Add(ServiceDefinition.FromType<LocalWorkService>());
            host.Initialize();
            if (!await host.Start())
            {
                throw new InvalidOperationException(Strings.LocalWorker_FailedToStart);
            }
            return host.GetInstance<LocalWorkService>();
        }
    }
}

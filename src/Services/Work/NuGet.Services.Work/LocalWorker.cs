using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.Work
{
    public static class LocalWorker
    {
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
            var service = new WorkService(host, InvocationQueue.Null);
            host.Services.Add(service);
            host.Initialize();
            if (!await host.Start())
            {
                throw new InvalidOperationException(Strings.LocalWorker_FailedToStart);
            }
            return service;
        }
    }
}

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
        public static async Task<WorkService> Create()
        {
            var host = new LocalServiceHost(
                new ServiceHostName(
                    new DatacenterName("local", 0),
                    "work"));
            var service = new WorkService(host, InvocationQueue.Null);
            host.Services.Add(service);
            await host.Initialize();
            if (!await host.Start())
            {
                throw new InvalidOperationException(Strings.LocalWorker_FailedToStart);
            }
            return service;
        }
    }
}

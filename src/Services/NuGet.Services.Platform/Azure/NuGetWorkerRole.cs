using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGet.Services.Azure
{
    public abstract class NuGetWorkerRole : RoleEntryPoint
    {
        private Func<NuGetService> _serviceFactory;
        private int _instanceCount;
        private AzureServiceHost _host;

        private NuGetService[] _instances;

        protected NuGetWorkerRole(Func<NuGetService> serviceFactory, int instanceCount)
        {
            _serviceFactory = serviceFactory;
            _instanceCount = instanceCount;
            _host = new AzureServiceHost();
        }

        public override void Run()
        {
            // Run all the workers
            Task.WaitAll(_instances.Select(i => i.Run()).ToArray());
        }

        public override void OnStop()
        {
            _host.Shutdown();
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // Start as many services as processors
            _instances = Enumerable
                .Range(0, _instanceCount)
                .Select(_ => StartService())
                .ToArray();

            // Start them all and wait for them all to finish starting successfully
            return Task.WhenAll(_instances.Select(i => i.Start())).Result.All(b => b);
        }

        private NuGetService StartService()
        {
            var service = _serviceFactory();
            service.Initialize(_host);
            return service;
        }
    }

    public abstract class NuGetWorkerRole<T> : NuGetWorkerRole
        where T : NuGetService, new()
    {
        public NuGetWorkerRole() : this(Environment.ProcessorCount) { }
        public NuGetWorkerRole(int instanceCount) : base(() => new T(), instanceCount) { }
    }
}

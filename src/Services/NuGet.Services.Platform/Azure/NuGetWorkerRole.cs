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
        private AzureServiceHost _host;
        private NuGetService[] _instances;

        protected NuGetWorkerRole()
        {
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
            _instances = CreateServices(_host).ToArray();

            // Start them all and wait for them all to finish starting successfully
            return Task.WhenAll(_instances.Select(i => i.Start())).Result.All(b => b);
        }

        protected abstract IEnumerable<NuGetService> CreateServices(NuGetServiceHost host);
    }
}

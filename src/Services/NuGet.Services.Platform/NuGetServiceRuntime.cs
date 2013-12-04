using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services
{
    public abstract class NuGetServiceRuntime
    {
        private static int _nextId = 0;

        private AzureTable<ServiceInstance> _instancesTable;
        private ServiceInstance _serviceInstance;

        public NuGetService Service { get; private set; }
        public string ServiceInstanceName { get; private set; }
        public virtual string TempDirectory { get; private set; }

        public NuGetServiceRuntime(NuGetService service)
        {
            var instanceId = Interlocked.Increment(ref _nextId) - 1;
            ServiceInstanceId.Set(instanceId);

            
            Service = service;
            TempDirectory = Path.Combine(Path.GetTempPath(), "NuGetServices", serviceName, instanceId.ToString());


            ServiceInstanceName = String.Format(
                CultureInfo.InvariantCulture,
                "{0}_{1}_{2}",
                Service.Host.HostInstanceName,
                Service.Name,
                ServiceInstanceId.Get());
        }

        public virtual async Task Start()
        {
            _instancesTable = Service.Storage.Primary.Tables.Table<ServiceInstance>();
            
            await StartInstanceTracing();

            await OnStart();
        }

        public virtual async Task Stop()
        {
            await OnStop();
        }

        public virtual async Task Run()
        {
            await OnRun();
        }

        protected virtual Task OnStart() { return Task.FromResult<object>(null); }
        protected virtual Task OnStop() { return Task.FromResult<object>(null); }
        protected abstract Task OnRun();

        private static class ServiceInstanceId
        {
            private const string RunnerIdDataName = "_NuGet_Service_Instance_Id";

            public static int Get()
            {
                return (int)CallContext.LogicalGetData(RunnerIdDataName);
            }

            public static void Set(int id)
            {
                CallContext.LogicalSetData(RunnerIdDataName, id);
            }
        }

        private Task StartInstanceTracing()
        {
            // Log Instance Status
            _serviceInstance = new ServiceInstance(
                ServiceInstanceName,
                Environment.MachineName,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
            return _instancesTable.InsertOrReplace(_serviceInstance);
        }
    }
}

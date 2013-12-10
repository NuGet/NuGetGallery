using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace NuGet.Services.Azure
{
    public abstract class NuGetWorkerRole : RoleEntryPoint
    {
        private AzureServiceHost _host;
        private Task _runTask;
        
        protected NuGetWorkerRole()
        {
            _host = new AzureServiceHost(this);
        }

        public override void Run()
        {
            _runTask = _host.Run();
            _runTask.Wait();
        }

        public override void OnStop()
        {
            _host.Shutdown();

            // As per http://msdn.microsoft.com/en-us/library/microsoft.windowsazure.serviceruntime.roleentrypoint.onstop.aspx
            // We need to block the thread that's running OnStop until the shutdown completes.
            _runTask.Wait();
        }

        public override bool OnStart()
        {
            // Initialize the host
            _host.Initialize();

            return _host.StartAndWait();
        }

        protected internal abstract IEnumerable<Type> GetServices();
    }
}

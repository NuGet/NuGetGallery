using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Microsoft.WindowsAzure.ServiceRuntime;
using NuGet.Services.Http;
using NuGet.Services.ServiceModel;

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
            try
            {
                _runTask = _host.Run();
                _runTask.Wait();
                ServicePlatformEventSource.Log.HostShutdownComplete(_host.Description.ServiceHostName.ToString());
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.FatalException(ex);
                throw;
            }
        }

        public override void OnStop()
        {
            try
            {
                _host.Shutdown();

                // As per http://msdn.microsoft.com/en-us/library/microsoft.windowsazure.serviceruntime.roleentrypoint.onstop.aspx
                // We need to block the thread that's running OnStop until the shutdown completes.
                if (_runTask != null)
                {
                    _runTask.Wait();
                }
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.FatalException(ex);
                throw;
            }
        }

        public override bool OnStart()
        {
            try
            {
                // Set up temp directory
                try
                {
                    var tempResource = RoleEnvironment.GetLocalResource("Temp");
                    if (tempResource != null)
                    {
                        Environment.SetEnvironmentVariable("TMP", tempResource.RootPath);
                        Environment.SetEnvironmentVariable("TEMP", tempResource.RootPath);
                    }
                }
                catch
                {
                    // Just ignore this. Use the default temp directory
                }

                // Initialize the host
                _host.Initialize();

                return _host.StartAndWait();
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.FatalException(ex);
                throw;
            }
        }

        protected internal abstract IEnumerable<ServiceDefinition> GetServices();
    }

    public abstract class SingleServiceWorkerRole<T> : NuGetWorkerRole
        where T : NuGetService
    {
        protected internal override IEnumerable<ServiceDefinition> GetServices()
        {
            yield return ServiceDefinition.FromType<T>();
        }
    }
}

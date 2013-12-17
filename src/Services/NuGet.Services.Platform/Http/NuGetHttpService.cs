using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.Hosting.Starter;
using NuGet.Services.ServiceModel;
using Owin;

namespace NuGet.Services.Http
{
    public abstract class NuGetHttpService : NuGetService
    {
        private IDisposable _httpServerLifetime;
        private TaskCompletionSource<object> _shutdownSource = new TaskCompletionSource<object>();

        protected NuGetHttpService(string serviceName, ServiceHost host) : base(serviceName, host) { }

        protected override Task<bool> OnStart()
        {
            var ep = Host.GetEndpoint("http");
            if (ep == null)
            {
                ServicePlatformEventSource.Log.MissingEndpoint(Host.Description.ServiceHostName.ToString(), InstanceName.ToString(), "http");
                return Task.FromResult(false); // Failed to start
            }

            // Set up start options
            var options = new StartOptions()
            {
                Port = ep.Port
            };
            ServicePlatformEventSource.Log.StartingHttpServices(Host.Description.ServiceHostName.ToString(), InstanceName.ToString(), ep.Port);
            try
            {
                _httpServerLifetime = WebApp.Start(options, Startup);
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.ErrorStartingHttpServices(Host.Description.ServiceHostName.ToString(), InstanceName.ToString(), ex);
                throw;
            }
            ServicePlatformEventSource.Log.StartedHttpServices(Host.Description.ServiceHostName.ToString(), InstanceName.ToString(), ep.Port);

            return base.OnStart();
        }

        protected override async Task OnRun()
        {
            // Sleep until cancelled
            await _shutdownSource.Task;
        }

        protected override void OnShutdown()
        {
            if (_httpServerLifetime != null)
            {
                _httpServerLifetime.Dispose();
            }
            _shutdownSource.SetResult(null);
        }

        protected abstract void Startup(IAppBuilder app);
    }
}

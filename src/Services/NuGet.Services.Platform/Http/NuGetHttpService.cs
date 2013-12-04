using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.Hosting.Starter;
using Owin;

namespace NuGet.Services.Http
{
    public abstract class NuGetHttpService : NuGetService
    {
        private IDisposable _httpServerLifetime;
        private TaskCompletionSource<object> _shutdownSource = new TaskCompletionSource<object>();

        protected NuGetHttpService(string serviceName, NuGetServiceHost host) : base(serviceName, host) { }

        protected override Task<bool> OnStart()
        {
            var ep = Host.GetEndpoint("http");
            if (ep == null)
            {
                ServicePlatformEventSource.Log.MissingEndpoint(Name, ServiceInstanceName, "http");
                return Task.FromResult(false); // Failed to start
            }

            // Set up start options
            var options = new StartOptions()
            {
                Port = ep.Port
            };
            ServicePlatformEventSource.Log.StartingHttpServices(Name, ServiceInstanceName, ep.Port);
            _httpServerLifetime = WebApp.Start(options, Startup);
            ServicePlatformEventSource.Log.StartedHttpServices(Name, ServiceInstanceName, ep.Port);

            return base.OnStart();
        }

        protected override async Task OnRun()
        {
            // Sleep until cancelled
            await _shutdownSource.Task;
        }

        protected override void OnShutdown()
        {
            _shutdownSource.SetResult(null);
        }

        protected abstract void Startup(IAppBuilder app);
    }
}

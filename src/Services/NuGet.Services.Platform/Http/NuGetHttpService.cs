using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
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
            var httpEndpoint = Host.GetEndpoint("http");
            var httpsEndpoint = Host.GetEndpoint("https");
            
            // Set up start options
            var options = new StartOptions();

            if (httpEndpoint != null)
            {
                options.Urls.Add("http://+:" + httpEndpoint.Port.ToString() + "/");
            }
            if (httpsEndpoint != null)
            {
                options.Urls.Add("https://+:" + httpsEndpoint.Port.ToString() + "/");
            }
            if (options.Urls.Count == 0)
            {
                ServicePlatformEventSource.Log.MissingHttpEndpoints(InstanceName);
                return Task.FromResult(false); // Failed to start
            }

            ServicePlatformEventSource.Log.StartingHttpServices(InstanceName, httpEndpoint, httpsEndpoint);
            try
            {
                _httpServerLifetime = WebApp.Start(options, Startup);
            }
            catch (Exception ex)
            {
                ServicePlatformEventSource.Log.ErrorStartingHttpServices(InstanceName, ex);
                throw;
            }
            ServicePlatformEventSource.Log.StartedHttpServices(InstanceName);

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

        protected virtual void Startup(IAppBuilder app)
        {
            app.Use(async (ctx, next) =>
            {
                await next();
                await Heartbeat();
            });
            Configure(app);
        }

        protected abstract void Configure(IAppBuilder app);

        public override void RegisterComponents(ContainerBuilder builder)
        {
            base.RegisterComponents(builder);

            builder.RegisterInstance(this).As<NuGetHttpService>();
        }
    }
}

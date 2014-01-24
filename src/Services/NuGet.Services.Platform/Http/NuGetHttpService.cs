using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.Hosting.Starter;
using NuGet.Services.ServiceModel;
using Owin;

namespace NuGet.Services.Http
{
    public abstract class NuGetHttpService : NuGetService
    {
        private TaskCompletionSource<object> _shutdownSource = new TaskCompletionSource<object>();

        public abstract PathString BasePath { get; }

        protected NuGetHttpService(ServiceName name, ServiceHost host) : base(name, host) { }

        public virtual void StartHttp(IAppBuilder app)
        {
            app.Use(async (ctx, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    ServicePlatformEventSource.Log.HttpException(ctx.Request.Uri.AbsoluteUri, ex);
                }
            });
            app.Use(async (ctx, next) =>
            {
                await next();
                Heartbeat();
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

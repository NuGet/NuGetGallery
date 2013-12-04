using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Http;
using NuGet.Services.Http;
using Owin;

namespace NuGet.Services.Jobs
{
    public class JobStatusHttpService : NuGetHttpService
    {
        public static readonly string MyServiceName = "JobsStatus";

        public JobStatusHttpService(NuGetServiceHost host)
            : base(MyServiceName, host)
        {
        }

        protected override void Startup(IAppBuilder app)
        {
            var config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();
            config.Services.Add(typeof(NuGetService), this);
            app.UseWebApi(config);
        }
    }
}

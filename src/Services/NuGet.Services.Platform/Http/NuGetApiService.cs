using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using NuGet.Services.ServiceModel;
using Owin;

namespace NuGet.Services.Http
{
    public class NuGetApiService : NuGetHttpService
    {
        public NuGetApiService(string name, ServiceHost host) : base(name, host) { }

        protected override void Startup(IAppBuilder app)
        {
            var config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();
            config.Services.Add(typeof(NuGetService), this);
            app.UseWebApi(config);
        }
    }
}

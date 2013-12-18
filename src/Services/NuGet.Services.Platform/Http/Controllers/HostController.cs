using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using NuGet.Services.Http.Models;

namespace NuGet.Services.Http.Controllers
{
    public class HostController : NuGetApiController
    {
        [Route("")]
        public ApiDescriptionModelBase GetDescription()
        {
            var description = Service.Describe();
            description.HostInfo =
                Url.RouteUri("Platform-Host-GetInfo", new Dictionary<string, object>());
            return description;
        }

        [Route("host", Name="Platform-Host-GetInfo")]
        public HostInfoResponseModel GetHostInfo()
        {
            return new HostInfoResponseModel(
                Host.Description,
                new AssemblyResponseModel(Host.RuntimeInformation))
                {
                    ServiceInstances = Url.RouteUri("Platform-Host-GetServices")
                };
        }

        [Route("host/services", Name="Platform-Host-GetServices")]
        public IEnumerable<ServiceInstanceResponseModel> GetServices()
        {
            return Host.Instances.Select(s => new ServiceInstanceResponseModel(s));
        }
    }
}
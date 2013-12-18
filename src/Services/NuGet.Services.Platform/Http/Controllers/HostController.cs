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
        public async Task<HostRootModel> GetDescription()
        {
            return new HostRootModel() {
                HostInfo = Url.RouteUri("Platform-Host-GetInfo", new Dictionary<string, object>()),
                ApiDescription = await Service.Describe()
            };
        }

        [Route("host", Name="Platform-Host-GetInfo")]
        public HostInformationModel GetHostInfo()
        {
            return new HostInformationModel(
                Host.Description,
                new AssemblyInformationModel(Host.RuntimeInformation))
                {
                    ServiceInstances = Url.RouteUri("Platform-Host-GetServices")
                };
        }

        [Route("host/services", Name="Platform-Host-GetServices")]
        public Task<ServiceInstanceModel[]> GetServices()
        {
            return Task.WhenAll(Host.Instances.Select(async s =>
            {
                var desc = await s.Describe();
                return new ServiceInstanceModel(s, desc);
            }));
        }
    }
}
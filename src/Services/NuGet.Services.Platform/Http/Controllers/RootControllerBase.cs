using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using NuGet.Services.Http.Models;

namespace NuGet.Services.Http.Controllers
{
    public class RootController : NuGetApiController
    {
        [Route("")]
        public async Task<HostRootModel> GetDescription()
        {
            return new HostRootModel() {
                Host = Url.RouteUri(Routes.GetHostInfo, new Dictionary<string, object>()),
                Api = await Service.GetApiModel(this)
            };
        }

        [Route("self", Name = Routes.GetHostInfo)]
        public HostInformationModel GetHostInfo()
        {
            return new HostInformationModel(
                Host.Description,
                Host.RuntimeInformation)
                {
                    ServiceInstances = Url.RouteUri(Routes.GetServices)
                };
        }

        [Route("self/services", Name = Routes.GetServices)]
        public Task<ServiceInstanceModel[]> GetServices()
        {
            return Task.WhenAll(Host.Instances.Select(async s =>
            {
                var desc = await s.Describe();
                var status = await s.GetCurrentStatus();
                return new ServiceInstanceModel(s, desc, status);
            }));
        }
    }
}
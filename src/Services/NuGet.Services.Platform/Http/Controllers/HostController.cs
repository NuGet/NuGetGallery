using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using NuGet.Services.Http.Models;

namespace NuGet.Services.Http.Controllers
{
    [RoutePrefix("host")]
    public class HostController : NuGetApiController
    {
        [Route("")]
        public HostResponseModel Get()
        {
            return new HostResponseModel(
                Host.Description,
                new AssemblyResponseModel(Host.RuntimeInformation));
        }
    }
}

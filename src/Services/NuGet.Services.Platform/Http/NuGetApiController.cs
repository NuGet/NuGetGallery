using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using NuGet.Services.Composition;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.Http
{
    public class NuGetApiController : ApiController
    {
        public ServiceHost Host { get; set; }
        public NuGetService Service { get; set; }
        public IComponentContainer Container { get; set; }

        public NuGetApiController()
        {
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using NuGet.Services.Http;

namespace NuGet.Services.Work.Api.Controllers
{
    public class RootController : NuGetApiController
    {
        [Route("")]
        public IHttpActionResult GetRoot()
        {
            return Content(HttpStatusCode.OK, "da work service!");
        }
    }
}

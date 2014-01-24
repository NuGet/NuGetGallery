using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using NuGet.Services.Http;
using NuGet.Services.Work.Api.Models;

namespace NuGet.Services.Work.Api.Controllers
{
    [RoutePrefix("workers")]
    [Authorize(Roles = Roles.Admin)]
    public class WorkersController : NuGetApiController
    {
        public InvocationQueue Queue { get; private set; }

        public WorkersController(InvocationQueue queue)
        {
            Queue = queue;
        }

        [Route("stats", Name = Routes.GetWorkerStatistics)]
        public async Task<IHttpActionResult> GetStatistics()
        {
            var stats = await Queue.GetInstanceStatistics();
            return Content(HttpStatusCode.OK, stats.Select(s => s.ToInstanceModel()));
        }
    }
}

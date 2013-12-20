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
    [RoutePrefix("instances")]
    public class InstancesController : NuGetApiController
    {
        public InvocationQueue Queue { get; private set; }

        public InstancesController(InvocationQueue queue)
        {
            Queue = queue;
        }

        [Route("stats", Name = Routes.GetInstanceStatistics)]
        public async Task<IHttpActionResult> GetStatistics()
        {
            var stats = await Queue.GetInstanceStatistics();
            return Content(HttpStatusCode.OK, stats.Select(s => new InstanceStatisticsModel(s)));
        }
    }
}

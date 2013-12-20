using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using NuGet.Services.Http;
using NuGet.Services.Work.Api.Models;
using NuGet.Services.Storage;

namespace NuGet.Services.Work.Api.Controllers
{
    [RoutePrefix("jobs")]
    public class JobsController : NuGetApiController
    {
        public StorageHub Storage { get; private set; }
        public InvocationQueue Queue { get; private set; }

        public JobsController(StorageHub storage, InvocationQueue queue)
        {
            Storage = storage;
            Queue = queue;
        }

        [Route("", Name = Routes.GetJobs)]
        public IEnumerable<JobDefinitionModel> Get()
        {
            return Storage.Primary.Tables.Table<JobDescription>().GetAll().Select(d => new JobDefinitionModel(d));
        }

        [Route("stats", Name = Routes.GetJobStatistics)]
        public async Task<IHttpActionResult> GetStatistics()
        {
            var stats = await Queue.GetJobStatistics();
            return Content(HttpStatusCode.OK, stats.Select(s => new JobStatisticsModel(s)));
        }
    }
}

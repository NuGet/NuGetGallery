using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using NuGet.Services.Http;
using NuGet.Services.Jobs.Api.Models;
using NuGet.Services.Storage;

namespace NuGet.Services.Jobs.Api.Controllers
{
    [RoutePrefix("jobs")]
    public class JobsController : NuGetApiController
    {
        public StorageHub Storage { get; private set; }

        public JobsController(StorageHub storage)
        {
            Storage = storage;
        }

        [Route("", Name = Routes.GetJobs)]
        public IEnumerable<JobDefinitionModel> Get()
        {
            return Storage.Primary.Tables.Table<JobDescription>().GetAll().Select(d => new JobDefinitionModel(d));
        }
    }
}

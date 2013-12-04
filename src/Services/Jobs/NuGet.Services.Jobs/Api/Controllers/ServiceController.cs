using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace NuGet.Services.Jobs.Api.Controllers
{
    [RoutePrefix("service")]
    public class ServiceController : NuGetApiController
    {
        [HttpGet]
        public async Task<HttpResponseMessage> Get()
        {
            // Get Data
            var instances = PrimaryStorage
                .Tables
                .Table<ServiceInstance>()
                .Get(Service.Host.Name);
                
            // Check authentication
            if (RequestContext.IsLocal || User.SafeIsAdmin())
            {
                return Ok(instances);
            }
            else
            {
                // Return simplified status
                return Ok(instances.Select(i => new
                {
                    i.Service,
                    i.BuildCommit,
                    i.BuildBranch,
                    i.BuildDate,
                    i.SourceCodeRepository
                })
                .GroupBy(x => x.Service)
                .Select(x => x.FirstOrDefault())
                .Where(x => x != null));
            }
        }
    }
}

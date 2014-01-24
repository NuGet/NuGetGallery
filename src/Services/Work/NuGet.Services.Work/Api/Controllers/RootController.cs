using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using NuGet.Services.Http;
using NuGet.Services.Work.Models;

namespace NuGet.Services.Work.Api.Controllers
{
    public class RootController : NuGetApiController
    {
        [Route("")]
        public IHttpActionResult GetRoot()
        {
            // Get the API Explorer service
            var explorer = Configuration.Services.GetApiExplorer();
            return Content(
                HttpStatusCode.OK,
                new
                {
                    Name = Service.Name.ToString(),
                    Service = Service.Name.Service,
                    Resources = new
                    {
                        Invocation = new
                        {
                            Get = 
                                Enumerable.Concat(
                                    new [] {
                                        Tuple.Create("detail", Url.RouteUri(Routes.GetSingleInvocation, new { id = "{id}" })),
                                        Tuple.Create("log", Url.RouteUri(Routes.GetInvocationLog, new { id = "{id}" })),
                                        Tuple.Create("active", Url.RouteUri(Routes.GetActiveInvocations))
                                    },
                                    Enum.GetValues(typeof(InvocationListCriteria)).OfType<InvocationListCriteria>()
                                        .Where(c => c != InvocationListCriteria.Active)
                                        .Select(c => Tuple.Create(c.ToString().ToLowerInvariant(), Url.RouteUri(Routes.GetInvocations, new { criteria = c.ToString().ToLowerInvariant() }))))
                                .ToDictionary(t => t.Item1, t => t.Item2),
                            Put = Url.RouteUri(Routes.PutInvocation),
                            Delete = Url.RouteUri(Routes.DeleteSingleInvocation, new { id = "{id}" }),
                            Stats = Url.RouteUri(Routes.GetInvocationStatistics)
                        },
                        Workers = new
                        {
                            Stats = Url.RouteUri(Routes.GetWorkerStatistics)
                        },
                        Jobs = new {
                            All = Url.RouteUri(Routes.GetJobs),
                            Stats = Url.RouteUri(Routes.GetJobStatistics)
                        }
                    }
                });
        }
    }
}

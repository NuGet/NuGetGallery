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
using NuGet.Services.Work.Models;

namespace NuGet.Services.Work.Api.Controllers
{
    [RoutePrefix("invocations")]
    [Authorize(Roles = Roles.Admin)]
    public class InvocationsController : NuGetApiController
    {
        public StorageHub Storage { get; private set; }
        public InvocationQueue Queue { get; private set; }

        public InvocationsController(StorageHub storage, InvocationQueue queue)
        {
            Storage = storage;
            Queue = queue;
        }

        [HttpGet]
        [Route("purgable", Name = Routes.GetPurgableInvocations)]
        public async Task<IHttpActionResult> GetPurgable(DateTimeOffset? since)
        {
            return Content(
                HttpStatusCode.OK, 
                (await Queue.GetPurgable(since ?? DateTimeOffset.UtcNow))
                .Select(i => i.ToModel()));
        }

        [Route("purgable", Name = Routes.DeletePurgableInvocations)]
        public Task<IHttpActionResult> DeletePurgableInvocations()
        {
            throw new NotImplementedException();
        }

        [Route("", Name = Routes.GetActiveInvocations)]
        public Task<IHttpActionResult> GetActive()
        {
            return Get(InvocationListCriteria.Active);
        }

        [Route("{criteria:invocationListCriteria}", Name = Routes.GetInvocations)]
        public async Task<IHttpActionResult> Get(InvocationListCriteria criteria)
        {
            if (!Enum.IsDefined(typeof(InvocationListCriteria), criteria))
            {
                return NotFound();
            }

            return Content(HttpStatusCode.OK, (await Queue.GetAll(criteria)).Select(i => i.ToModel()));
        }

        [Route("{id}", Name = Routes.GetSingleInvocation)]
        public async Task<IHttpActionResult> Get(Guid id)
        {
            var invocation = await Queue.Get(id);
            if (invocation == null)
            {
                return NotFound();
            }
            return Content(HttpStatusCode.OK, invocation.ToModel());
        }

        [Route("", Name = Routes.PutInvocation)]
        public async Task<IHttpActionResult> Put([FromBody] InvocationRequest request)
        {
            var invocation = await Queue.Enqueue(
                request.Job, 
                request.Source ?? Constants.Source_Unknown, 
                request.Payload, 
                request.VisibilityDelay ?? TimeSpan.Zero);
            if (invocation == null)
            {
                return StatusCode(HttpStatusCode.ServiceUnavailable);
            }
            else
            {
                return Content(HttpStatusCode.Created, invocation.ToModel());
            }
        }

        [Route("{id}", Name = Routes.DeleteSingleInvocation)]
        public async Task Delete(Guid id)
        {
            await Queue.Purge(id);
        }

        [Route("stats", Name = Routes.GetInvocationStatistics)]
        public async Task<IHttpActionResult> GetStatistics()
        {
            var stats = await Queue.GetStatistics();
            if (stats == null)
            {
                return StatusCode(HttpStatusCode.ServiceUnavailable);
            }
            else
            {
                return Content(HttpStatusCode.OK, stats.ToInvocationModel());
            }
        }
    }
}

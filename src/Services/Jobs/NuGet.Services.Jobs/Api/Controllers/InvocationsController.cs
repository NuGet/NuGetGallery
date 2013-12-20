using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using NuGet.Services.Http;
using NuGet.Services.Jobs.Api.Models;
using NuGet.Services.Storage;

namespace NuGet.Services.Jobs.Api.Controllers
{
    [RoutePrefix("invocations")]
    public class InvocationsController : NuGetApiController
    {
        public StorageHub Storage { get; private set; }
        public InvocationQueue Queue { get; private set; }

        public InvocationsController(StorageHub storage, InvocationQueue queue)
        {
            Storage = storage;
            Queue = queue;
        }

        [Route("{criteria:alpha?}", Name = Routes.GetInvocations)]
        public async Task<IHttpActionResult> Get(InvocationListCriteria criteria = InvocationListCriteria.Active)
        {
            if (!Enum.IsDefined(typeof(InvocationListCriteria), criteria))
            {
                return NotFound();
            }

            return Content(HttpStatusCode.OK, (await Queue.GetAll(criteria)).Select(i => new InvocationResponseModel(i)));
        }

        [Route("{id}", Name = Routes.GetSingleInvocation)]
        public async Task<IHttpActionResult> Get(Guid id)
        {
            var invocation = await Queue.Get(id);
            if (invocation == null)
            {
                return NotFound();
            }
            return Content(HttpStatusCode.OK, invocation);
        }

        [Route("", Name = Routes.PutInvocation)]
        public async Task<IHttpActionResult> Put([FromBody] InvocationRequestModel request)
        {
            var invocation = await Queue.Enqueue(
                request.Job, 
                request.Source, 
                request.Payload, 
                request.VisibilityDelay ?? TimeSpan.Zero);
            if (invocation == null)
            {
                return StatusCode(HttpStatusCode.ServiceUnavailable);
            }
            else
            {
                return Content(HttpStatusCode.Created, new InvocationResponseModel(invocation));
            }
        }
    }
}

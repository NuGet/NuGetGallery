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

        [Route("", Name = Routes.GetInvocations)]
        public IEnumerable<InvocationSummaryModel> Get()
        {
            return Storage.Primary.Tables.Table<Invocation>().GetAll().Select(i => new InvocationSummaryModel(i) {
                Detail = Url.RouteUri(Routes.GetSingleInvocation, new { id = i.Id })
            });
        }

        [Route("{id}", Name = Routes.GetSingleInvocation)]
        public async Task<Invocation> Get(Guid id)
        {
            var invocation = await Storage.Primary.Tables.Table<Invocation>().Get(id.ToString("N").ToLowerInvariant(), String.Empty);
            return invocation;
        }

        [Route("", Name = Routes.PutInvocation)]
        public async Task<InvocationSummaryModel> Put([FromBody] InvocationRequestModel request)
        {
            var invocation = new Invocation(Guid.NewGuid(), request.Job, request.Source, request.Payload);
            await Queue.Enqueue(invocation);
            return new InvocationSummaryModel(invocation) {
                Detail = Url.RouteUri(Routes.GetSingleInvocation, new { id = invocation.Id })
            };
        }
    }
}

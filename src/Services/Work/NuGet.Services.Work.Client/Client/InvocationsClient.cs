using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Client;
using NuGet.Services.Models;
using NuGet.Services.Work.Models;

namespace NuGet.Services.Work.Client
{
    public class InvocationsClient
    {
        private HttpClient _client;

        public InvocationsClient(HttpClient client)
        {
            _client = client;
        }

        public Task<ServiceResponse<Invocation>> Put(InvocationRequest request)
        {
            return _client.PutAsync(
                "work/invocations",
                new ObjectContent<InvocationRequest>(
                    request,
                    JsonFormat.Formatter))
                .AsServiceResponse<Invocation>();
        }

        public Task<ServiceResponse> GetLog(string id)
        {
            return _client.GetAsync(
                "work/invocations/" + id.ToLowerInvariant() + "/log")
                .AsServiceResponse();
        }

        public Task<ServiceResponse<IEnumerable<Invocation>>> Get(InvocationListCriteria criteria)
        {
            return GetInvocations(criteria, String.Empty);
        }

        public Task<ServiceResponse<IEnumerable<Invocation>>> Get(InvocationListCriteria criteria, int limit)
        {
            return GetInvocations(criteria, "?limit=" + limit.ToString());
        }

        public Task<ServiceResponse<IEnumerable<Invocation>>> GetPurgable(DateTimeOffset? before)
        {
            string beforeValue = RenderBeforeQueryStringValue(before);
            return _client.GetAsync(
                "work/invocations/purgable" + beforeValue)
                .AsServiceResponse<IEnumerable<Invocation>>();
        }

        public Task<ServiceResponse<IEnumerable<Invocation>>> Purge(DateTimeOffset? before)
        {
            string beforeValue = RenderBeforeQueryStringValue(before);
            return _client.DeleteAsync(
                "work/invocations/purgable" + beforeValue)
                .AsServiceResponse<IEnumerable<Invocation>>();
        }

        public Task<ServiceResponse<Invocation>> Get(string id)
        {
            return _client.GetAsync("work/invocations/" + id).AsServiceResponse<Invocation>();
        }

        public Task<ServiceResponse<InvocationStatistics>> GetStatistics()
        {
            return _client.GetAsync("work/invocations/stats").AsServiceResponse<InvocationStatistics>();
        }

        private Task<ServiceResponse<IEnumerable<Invocation>>> GetInvocations(InvocationListCriteria criteria, string queryString)
        {
            return _client.GetAsync(
                "work/invocations/" + criteria.ToString().ToLowerInvariant() + queryString)
                .AsServiceResponse<IEnumerable<Invocation>>();
        }

        private static string RenderBeforeQueryStringValue(DateTimeOffset? before)
        {
            string beforeValue = String.Empty;
            if (before != null)
            {
                beforeValue = "?before=" + WebUtility.UrlEncode(before.Value.ToString("O"));
            }
            return beforeValue;
        }
    }
}

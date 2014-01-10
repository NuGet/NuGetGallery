using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.Client
{
    public class ServiceResponse : ServiceResponse<string>
    {
        public ServiceResponse(HttpResponseMessage httpResponse) : base(httpResponse) { }
        
        public override Task<string> ReadContent()
        {
            return HttpResponse.Content.ReadAsStringAsync();
        }
    }

    public class ServiceResponse<T>
    {
        public HttpResponseMessage HttpResponse { get; private set; }
        public HttpStatusCode StatusCode { get { return HttpResponse.StatusCode; } }
        public bool IsSuccessStatusCode { get { return HttpResponse.IsSuccessStatusCode; } }
        public string ReasonPhrase { get { return HttpResponse.ReasonPhrase; } }

        public ServiceResponse(HttpResponseMessage httpResponse)
        {
            HttpResponse = httpResponse;
        }

        public virtual Task<T> ReadContent()
        {
            return HttpResponse.Content.ReadAsAsync<T>();
        }
    }

    public static class HttpResponseExtensions
    {
        public static ServiceResponse AsServiceResponse(this HttpResponseMessage self)
        {
            return new ServiceResponse(self);
        }

        public static ServiceResponse<T> AsServiceResponse<T>(this HttpResponseMessage self)
        {
            return new ServiceResponse<T>(self);
        }

        public static async Task<ServiceResponse> AsServiceResponse(this Task<HttpResponseMessage> self)
        {
            return new ServiceResponse(await self);
        }

        public static async Task<ServiceResponse<T>> AsServiceResponse<T>(this Task<HttpResponseMessage> self)
        {
            return new ServiceResponse<T>(await self);
        }
    }
}

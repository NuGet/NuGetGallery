using System.Net;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class HttpStatusCodeWithBodyResult : HttpStatusCodeResult
    {
        private readonly string body;

        public HttpStatusCodeWithBodyResult(HttpStatusCode statusCode, string statusDescription)
            : this(statusCode, statusDescription, statusDescription)
        {

        }

        public HttpStatusCodeWithBodyResult(HttpStatusCode statusCode, string statusDescription, string body)
            : base((int)statusCode, statusDescription)
        {
            this.body = body;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            base.ExecuteResult(context);
            var response = context.RequestContext.HttpContext.Response;
            response.Write(body);
        }
    }
}
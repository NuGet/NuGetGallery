using System;
using System.Web.Mvc;

namespace NuGetGallery
{
    public class HttpHeaderValueProviderFactory : ValueProviderFactory
    {
        public override IValueProvider GetValueProvider(ControllerContext controllerContext)
        {
            var request = controllerContext.RequestContext.HttpContext.Request;
            
            // Use this value provider only if a route is located under "API"
            if (request.Path.TrimStart('/').StartsWith("api", StringComparison.OrdinalIgnoreCase))
                return new HttpHeaderValueProvider(request, "ApiKey");
            return null;
        }
    }
}
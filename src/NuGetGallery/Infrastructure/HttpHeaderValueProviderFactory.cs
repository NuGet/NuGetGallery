// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Web.Mvc;

namespace NuGetGallery
{
    public class HttpHeaderValueProviderFactory : ValueProviderFactory
    {
        public override IValueProvider GetValueProvider(ControllerContext controllerContext)
        {
            var request = controllerContext.RequestContext.HttpContext.Request;

            // Use this value provider only if a route is located under "API"
            if (controllerContext.Controller is ApiController)
            {
                return new HttpHeaderValueProvider(request, "ApiKey");
            }
            return null;
        }
    }
}
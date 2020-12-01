// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;
using NuGet.Services.AzureSearch;
using NuGet.Services.AzureSearch.SearchService;

namespace NuGet.Services.SearchService
{
    public class ApiExceptionFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            switch (context.Exception)
            {
                case AzureSearchException _:
                    context.Response = context.Request.CreateResponse(
                        HttpStatusCode.ServiceUnavailable,
                        new ErrorResponse("The service is unavailable."));
                    break;

                case InvalidSearchRequestException isre:
                    context.Response = context.Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        new ErrorResponse(isre.Message));
                    break;
            }
        }
    }
}
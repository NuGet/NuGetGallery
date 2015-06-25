// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Http;
using System.Web.Http.Results;

namespace NuGetGallery
{
    public static class HttpActionResultExtensions
    {
        public static T AsContent<T>(this IHttpActionResult actionResult)
        {
            var negotiatedContentResult = actionResult as OkNegotiatedContentResult<T>;
            if (negotiatedContentResult != null)
            {
                return negotiatedContentResult.Content;
            }
            return default(T);
        }
    }
}

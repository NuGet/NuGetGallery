// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Authentication;

namespace NuGetGallery.Filters
{
    public sealed class ApiAuthorizeAttribute : AuthorizeAttribute
    {
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var owinContext = filterContext.HttpContext.GetOwinContext();
            owinContext.Authentication.Challenge(AuthenticationTypes.ApiKey);
            owinContext.Response.StatusCode = 401;
            filterContext.Result = new HttpUnauthorizedResult();
        }
    }
}
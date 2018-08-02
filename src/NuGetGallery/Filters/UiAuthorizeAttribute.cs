// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Claims;
using System.Web.Mvc;
using System.Web.Routing;
using AuthorizationContext = System.Web.Mvc.AuthorizationContext;

namespace NuGetGallery.Filters
{
    public sealed class UIAuthorizeAttribute : AuthorizeAttribute
    {
        public bool AllowDiscontinuedLogins { get; }

        public UIAuthorizeAttribute(bool allowDiscontinuedLogins = false)
        {
            AllowDiscontinuedLogins = allowDiscontinuedLogins;
        }

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            // If the user has a discontinued login claim, redirect them to the homepage
            var identity = filterContext.HttpContext.User.Identity as ClaimsIdentity;
            if (!AllowDiscontinuedLogins && ClaimsExtensions.HasDiscontinuedLoginClaims(identity))
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(
                        new
                        {
                            area = "",
                            controller = "Pages",
                            action = "Home"
                        }));
            }

            base.OnAuthorization(filterContext);
        }
    }
}
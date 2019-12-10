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
            // If the user has a discontinued login claim or should enable 2FA, redirect them to the homepage
            var identity = filterContext.HttpContext.User.Identity as ClaimsIdentity;
            var askUserToEnable2FA = filterContext.Controller?.TempData?.ContainsKey(GalleryConstants.AskUserToEnable2FA);

            if ((!AllowDiscontinuedLogins && ClaimsExtensions.HasDiscontinuedLoginClaims(identity))
                || (askUserToEnable2FA.HasValue && askUserToEnable2FA.Value))
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
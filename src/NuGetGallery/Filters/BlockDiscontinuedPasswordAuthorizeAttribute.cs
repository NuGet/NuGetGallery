// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Claims;
using System.Web.Mvc;
using System.Web.Routing;
using NuGetGallery.Authentication;
using AuthenticationTypes = NuGetGallery.Authentication.AuthenticationTypes;
using AuthorizationContext = System.Web.Mvc.AuthorizationContext;

namespace NuGetGallery.Filters
{
    public sealed class BlockDiscontinuedPasswordAuthorizeAttribute : AuthorizeAttribute
    {
        
        public BlockDiscontinuedPasswordAuthorizeAttribute()
        {}

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            // If a password credential was used, and the user has a discontinued password claim, redirect them to the homepage.
            var identity = filterContext.HttpContext.User.Identity as ClaimsIdentity;
            if (identity != null && 
                identity.IsAuthenticated && 
                identity.AuthenticationType == AuthenticationTypes.LocalUser)
            {
                var discontinuedPasswordClaim = identity.GetClaimOrDefault(NuGetClaims.DiscontinuedPassword);
                if (NuGetClaims.DiscontinuedPasswordValue.Equals(discontinuedPasswordClaim, StringComparison.OrdinalIgnoreCase))
                {
                    filterContext.Result = new RedirectToRouteResult(
                        new RouteValueDictionary(
                            new
                            {
                                controller = "Pages",
                                action = "Home"
                            }));
                }
            }

            base.OnAuthorization(filterContext);
        }
    }
}
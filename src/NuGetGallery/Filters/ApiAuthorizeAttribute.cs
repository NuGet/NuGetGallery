// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using AuthenticationTypes = NuGetGallery.Authentication.AuthenticationTypes;
using AuthorizationContext = System.Web.Mvc.AuthorizationContext;

namespace NuGetGallery.Filters
{
    public sealed class ApiAuthorizeAttribute : AuthorizeAttribute
    {
        
        public ApiAuthorizeAttribute()
        {}

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            // Add a warning header if the API key is about to expire (or has expired)
            var identity = filterContext.HttpContext.User.Identity as ClaimsIdentity;
            var controller = filterContext.Controller as AppController;
            if (identity != null && identity.IsAuthenticated && identity.AuthenticationType == AuthenticationTypes.ApiKey && controller != null)
            {
                var user = controller.GetCurrentUser();
                var apiKeyCredential =  user.GetCurrentApiKeyCredential(identity);

                if (apiKeyCredential != null && apiKeyCredential.Expires.HasValue)
                {
                    var accountUrl = controller.Url.ManageMyApiKeys(false);

                    var expirationPeriod = apiKeyCredential.Expires.Value - DateTime.UtcNow;
                    if (apiKeyCredential.HasExpired)
                    {
                        // expired warning
                        filterContext.HttpContext.Response.Headers.Add(
                            GalleryConstants.WarningHeaderName,
                            string.Format(CultureInfo.InvariantCulture, Strings.WarningApiKeyExpired, accountUrl));
                    }
                    else if (expirationPeriod.TotalDays <= controller.NuGetContext.Config.Current.WarnAboutExpirationInDaysForApiKeyV1)
                    {
                        // about to expire warning
                        var expirationDays = Math.Round(expirationPeriod.TotalDays, 0);
                        var expiration = expirationDays + " " + (expirationDays == 1 ? "day" : "days");

                        filterContext.HttpContext.Response.Headers.Add(
                            GalleryConstants.WarningHeaderName,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                Strings.WarningApiKeyAboutToExpire,
                                expiration,
                                accountUrl));
                    }
                }
            }

            base.OnAuthorization(filterContext);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var owinContext = filterContext.HttpContext.GetOwinContext();
            owinContext.Authentication.Challenge(AuthenticationTypes.ApiKey);
            owinContext.Response.StatusCode = 401;
            filterContext.Result = new HttpUnauthorizedResult();
        }
    }
}
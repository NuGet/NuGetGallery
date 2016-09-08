﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Authentication;
using AuthenticationTypes = NuGetGallery.Authentication.AuthenticationTypes;
using AuthorizationContext = System.Web.Mvc.AuthorizationContext;

namespace NuGetGallery.Filters
{
    public sealed class ApiAuthorizeAttribute : AuthorizeAttribute
    {
        public override async void OnAuthorization(AuthorizationContext filterContext)
        {
            // Add a warning header if the API key is about to expire (or has expired)
            var identity = filterContext.HttpContext.User.Identity as ClaimsIdentity;
            var controller = filterContext.Controller as AppController;
            if (identity != null && identity.IsAuthenticated && identity.AuthenticationType == AuthenticationTypes.ApiKey && controller != null)
            {
                var apiKey = identity.GetClaimOrDefault(NuGetClaims.ApiKey);
                
                var user = controller.GetCurrentUser();

                var apiKeyCredential = user.Credentials.FirstOrDefault(c => c.Value == apiKey);
                if (apiKeyCredential != null && apiKeyCredential.Expires.HasValue)
                {
                    var configService = controller.NuGetContext.Config;
                    var currentConfig = await configService.GetCurrent();

                    var accountUrl = (configService.GetSiteRoot(currentConfig.RequireSSL)).TrimEnd('/') + "/account";

                    var expirationPeriod = apiKeyCredential.Expires.Value - DateTime.UtcNow;
                    if (apiKeyCredential.HasExpired)
                    {
                        // expired warning
                        filterContext.HttpContext.Response.Headers.Add(
                            Constants.WarningHeaderName,
                            string.Format(CultureInfo.InvariantCulture, Strings.WarningApiKeyExpired, accountUrl));
                    }
                    else if (expirationPeriod.TotalDays <= currentConfig.WarnAboutExpirationInDaysForApiKeyV1)
                    {
                        // about to expire warning
                        filterContext.HttpContext.Response.Headers.Add(
                            Constants.WarningHeaderName,
                            string.Format(CultureInfo.InvariantCulture, Strings.WarningApiKeyAboutToExpire, expirationPeriod.TotalDays, accountUrl));
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
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Security;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class FormsAuthenticationService : IFormsAuthenticationService
    {
        private readonly IGalleryConfigurationService _configService;

        public FormsAuthenticationService(IGalleryConfigurationService configService)
        {
            _configService = configService;
        }

        private const string ForceSSLCookieName = "ForceSSL";

        public async void SetAuthCookie(
            string userName,
            bool createPersistentCookie,
            IEnumerable<string> roles)
        {
            string formattedRoles = String.Empty;
            if (roles.AnySafe())
            {
                formattedRoles = String.Join("|", roles);
            }

            HttpContext context = HttpContext.Current;

            var ticket = new FormsAuthenticationTicket(
                version: 1,
                name: userName,
                issueDate: DateTime.UtcNow,
                expiration: DateTime.UtcNow.AddYears(200),
                isPersistent: createPersistentCookie,
                userData: formattedRoles
                );

            string encryptedTicket = FormsAuthentication.Encrypt(ticket);
            var formsCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
            {
                HttpOnly = true,
                Secure = (await _configService.GetCurrent()).RequireSSL
            };
            context.Response.Cookies.Add(formsCookie);

            if ((await _configService.GetCurrent()).RequireSSL)
            {
                // Drop a second cookie indicating that the user is logged in via SSL (no secret data, just tells us to redirect them to SSL)
                HttpCookie responseCookie = new HttpCookie(ForceSSLCookieName, "true");
                responseCookie.HttpOnly = true;
                context.Response.Cookies.Add(responseCookie);
            }
        }

        public void SignOut()
        {
            FormsAuthentication.SignOut();

            // Delete the "LoggedIn" cookie
            HttpContext context = HttpContext.Current;
            var cookie = context.Request.Cookies[ForceSSLCookieName];
            if (cookie != null)
            {
                cookie.Expires = DateTime.UtcNow.AddDays(-1d);
                context.Response.Cookies.Add(cookie);
            }
        }


        public bool ShouldForceSSL(HttpContextBase context)
        {
            var cookie = context.Request.Cookies[ForceSSLCookieName];
            
            bool value;
            if (cookie != null && Boolean.TryParse(cookie.Value, out value))
            {
                return value;
            }
            
            return false;
        }
    }
}
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
        private readonly IAppConfiguration _configuration;

        public FormsAuthenticationService(IAppConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void SetAuthCookie(
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
                Secure = _configuration.RequireSSL
            };
            context.Response.Cookies.Add(formsCookie);
        }

        public void SignOut()
        {
            FormsAuthentication.SignOut();
        }
    }
}
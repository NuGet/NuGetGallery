// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Claims;
using System.Web.Mvc;
using Microsoft.Owin;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public static class IOwinContextExtensions
    {
        internal static readonly string CurrentUserOwinEnvironmentKey = "nuget.user";

        // This is a method because the first call will perform a database call
        /// <summary>
        /// Get the current user, from the database, or if someone in this request has already
        /// retrieved it, from memory. This will NEVER return null. It will throw an exception
        /// that will yield an HTTP 401 if it would return null. As a result, it should only
        /// be called in actions with the Authorize attribute or a Request.IsAuthenticated check
        /// </summary>
        /// <returns>The current user</returns>
        public static User GetCurrentUser(this IOwinContext self)
        {
            if (self.Request.User == null ||
                (self.Request.User.Identity != null && !self.Request.User.Identity.IsAuthenticated))
            {
                return null;
            }

            User user = null;
            object obj;
            if (self.Environment.TryGetValue(CurrentUserOwinEnvironmentKey, out obj))
            {
                user = obj as User;
            }

            if (user == null)
            {
                user = LoadUser(self);
                self.Environment[CurrentUserOwinEnvironmentKey] = user;
            }

            if (user == null)
            {
                // Unauthorized! If we get here it's because a valid session token was presented, but the
                // user doesn't exist any more. So we just have a generic error.
                self.Authentication.SignOut();
                throw new CurrentUserDeletedException();
            }

            return user;
        }

        private static User LoadUser(IOwinContext context)
        {
            var principal = context.Authentication.User;
            if (principal != null)
            {
                // Try to authenticate with the user name
                string userName = principal.GetClaimOrDefault(ClaimTypes.Name);

                if (!String.IsNullOrEmpty(userName))
                {
                    var user = DependencyResolver.Current
                        .GetService<IUserService>()
                        .FindByUsername(userName);

                    // Try to add the tenant ID information as an additional claim since we have the full user record
                    // and the associated credentials.
                    if (user != null && principal.Identity is ClaimsIdentity identity)
                    {
                        // From the schema, it is possible to have multiple credentials. Prefer the latest one.
                        var externalCredential = user
                            .Credentials
                            .OrderByDescending(x => x.Created)
                            .FirstOrDefault(c => c.IsExternal() && c.TenantId != null);

                        if (externalCredential != null)
                        {
                            identity.TryAddClaim(MicrosoftClaims.TenantId, externalCredential.TenantId);
                        }
                    }

                    return user;
                }
            }
            return null; // No user logged in, or credentials could not be resolved
        }
    }
}

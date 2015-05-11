// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.Azure.ActiveDirectory.GraphClient.Extensions;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public static class PackageRegistrationsImpl
    {
        public static async Task ListPackageRegistrations(IOwinContext context)
        {
            //
            // The Scope claim tells you what permissions the client application has in the service.
            // In this case we look for a scope value of user_impersonation, or full access to the service as the user.
            //
            if (ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/scope").Value != "user_impersonation")
            {
                await context.Response.WriteAsync("The Scope claim does not contain 'user_impersonation' or scope claim not found");
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            }

            ActiveDirectoryClient activeDirectoryClient = await ServiceHelpers.GetActiveDirectoryClient();

            string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;

            IUser user = await activeDirectoryClient.Users.GetByObjectId(userObjectID).ExecuteAsync();

            IPagedCollection<IDirectoryObject> groups = await ((IUserFetcher)user).MemberOf.ExecuteAsync();

            JArray array = new JArray();

            while (true)
            {
                foreach (IDirectoryObject group in groups.CurrentPage)
                {
                    array.Add(((Group)group).DisplayName);
                }

                if (!groups.MorePagesAvailable)
                {
                    break;
                }

                groups = await groups.GetNextPageAsync();
            }

            await ServiceHelpers.WriteResponse(context, array, HttpStatusCode.OK);
        }
    }
}
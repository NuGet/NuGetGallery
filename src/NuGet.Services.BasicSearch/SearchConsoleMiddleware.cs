// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Owin;

namespace NuGet.Services.BasicSearch
{
    public class SearchConsoleMiddleware
        : OwinMiddleware
    {
        public SearchConsoleMiddleware(OwinMiddleware next)
            : base(next)
        {
        }

        public override async Task Invoke(IOwinContext context)
        {
            if (string.Equals(context.Request.Path.Value, "/console", StringComparison.OrdinalIgnoreCase))
            {
                // Redirect to trailing slash to maintain relative links
                context.Response.Redirect(context.Request.PathBase + context.Request.Path + "/");
                context.Response.StatusCode = 301;

                return;
            }
            else if (string.Equals(context.Request.Path.Value, "/console/", StringComparison.OrdinalIgnoreCase))
            {
                context.Request.Path = new PathString("/console/Index.html");
            }

            // Run all the things
            await Next.Invoke(context);
        }
    }
}
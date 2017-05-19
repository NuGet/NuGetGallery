// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Owin;

namespace NuGet.Services.Owin
{
    public class ForceSslMiddleware : OwinMiddleware
    {
        private readonly int _sslPort;

        public ForceSslMiddleware(OwinMiddleware next, int sslPort) : base(next)
        {
            _sslPort = sslPort;
        }

        public override async Task Invoke(IOwinContext context)
        {
            if (!context.Request.IsSecure)
            {
                if (context.Request.Method == HttpMethod.Get.Method || context.Request.Method == HttpMethod.Head.Method)
                {
                    context.Response.Redirect(new UriBuilder(context.Request.Uri)
                    {
                        Scheme = Uri.UriSchemeHttps,
                        Port = _sslPort
                    }.Uri.AbsoluteUri);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.ReasonPhrase = "SSL Required";
                }
            }
            else
            {
                await Next.Invoke(context);
            }
        }
    }
}

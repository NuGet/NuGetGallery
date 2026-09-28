// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NuGet.Services.CDNRedirect.Services;

namespace NuGet.Services.CDNRedirect.Middleware
{
    public class RedirectMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IRedirectService _redirectService;
        private readonly ILogger<RedirectMiddleware> _logger;

        public RedirectMiddleware(
            RequestDelegate next,
            IRedirectService redirectService,
            ILogger<RedirectMiddleware> logger)
        {
            _next = next;
            _redirectService = redirectService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IErrorViewRenderer errorViewRenderer)
        {
            if (context.Request.Path.StartsWithSegments("/Status"))
            {
                await _next(context);
                return;
            }

            try
            {
                var redirectUri = _redirectService.GetRedirectUri(context.Request);
                context.Response.Redirect(redirectUri.AbsoluteUri, permanent: false, preserveMethod: false);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unhandled exception while processing {RequestPath}.",
                    context.Request.Path);
                await errorViewRenderer.RenderAsync(context, context.RequestAborted);
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace NuGetGallery.Middleware
{
    /// <summary>
    /// ASP.NET Core middleware for forcing HTTPS redirection with exclusion paths
    /// Migrated from NuGet.Services.Owin ForceSslMiddleware
    /// </summary>
    public class ForceSslMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly int _sslPort;
        private readonly HashSet<string> _exclusionPaths;

        public ForceSslMiddleware(RequestDelegate next, int sslPort, string? exclusionPaths = null)
        {
            _next = next;
            _sslPort = sslPort;
            _exclusionPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(exclusionPaths))
            {
                foreach (var path in exclusionPaths.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    _exclusionPaths.Add(path.Trim());
                }
            }
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Check if path is excluded
            var path = context.Request.Path.Value ?? string.Empty;
            if (_exclusionPaths.Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            // Check if already HTTPS
            if (context.Request.IsHttps)
            {
                await _next(context);
                return;
            }

            // Redirect to HTTPS
            var host = context.Request.Host;
            if (_sslPort != 443)
            {
                host = new HostString(host.Host, _sslPort);
            }
            else
            {
                host = new HostString(host.Host);
            }

            var httpsUrl = $"https://{host}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
            context.Response.Redirect(httpsUrl, permanent: true);
        }
    }
}

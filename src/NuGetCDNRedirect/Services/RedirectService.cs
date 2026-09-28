// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NuGet.Services.CDNRedirect.Configuration;

namespace NuGet.Services.CDNRedirect.Services
{
    public class RedirectService : IRedirectService
    {
        private readonly Uri _redirectDestination;

        public RedirectService(IOptions<RedirectOptions> options)
        {
            _redirectDestination = new Uri(options.Value.RedirectDestination!, UriKind.Absolute);
        }

        public Uri GetRedirectUri(HttpRequest request)
        {
            return new UriBuilder(_redirectDestination)
            {
                Path = request.PathBase.Add(request.Path).Value ?? "/",
                Query = request.QueryString.Value?.TrimStart('?') ?? string.Empty
            }.Uri;
        }
    }
}

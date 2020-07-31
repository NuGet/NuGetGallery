// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    /// <summary>
    /// A <see cref="HttpMessageHandler"/> that prevents queries to the V2 feed from being hijacked by the search service.
    /// </summary>
    public class NonhijackedV2HttpMessageHandler : DelegatingHandler
    {
        /// <param name="inner">The <see cref="HttpMessageHandler"/> to wrap.</param>
        public NonhijackedV2HttpMessageHandler(HttpMessageHandler inner)
            : base(inner)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri = UriUtils.GetNonhijackableUri(request.RequestUri);
            return base.SendAsync(request, cancellationToken);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ng
{
    public class StorageAccessHandler : DelegatingHandler
    {
        private readonly string _catalogBaseAddress;
        private readonly string _storageBaseAddress;

        public StorageAccessHandler(string catalogBaseAddress, string storageBaseAddress, HttpMessageHandler handler)
            : base(handler)
        {
            _catalogBaseAddress = catalogBaseAddress;
            _storageBaseAddress = storageBaseAddress;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string requestUri = request.RequestUri.AbsoluteUri;

            if (requestUri.StartsWith(_catalogBaseAddress))
            {
                string newRequestUri = _storageBaseAddress + requestUri.Substring(_catalogBaseAddress.Length);
                request.RequestUri = new Uri(newRequestUri);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}

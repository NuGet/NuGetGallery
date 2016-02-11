// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace NuGetGallery.WebApi
{
    public class PlainTextResult
        : IHttpActionResult
    {
        private readonly HttpRequestMessage _request;
        public string Content { get; private set; }

        public PlainTextResult(string content, HttpRequestMessage request)
        {
            _request = request;
            Content = content;
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage()
            {
                Content = new StringContent(Content, Encoding.UTF8, "text/plain"),
                RequestMessage = _request
            };
            return Task.FromResult(response);
        }
    }
}
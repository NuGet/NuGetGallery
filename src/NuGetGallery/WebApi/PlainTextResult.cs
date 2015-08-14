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
        private readonly string _content;
        private readonly HttpRequestMessage _request;

        public PlainTextResult(string content, HttpRequestMessage request)
        {
            _content = content;
            _request = request;
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage()
            {
                Content = new StringContent(_content, Encoding.UTF8, "text/plain"),
                RequestMessage = _request
            };
            return Task.FromResult(response);
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
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

        public HttpStatusCode StatusCode { get; private set; }

        public PlainTextResult(string content, HttpRequestMessage request, HttpStatusCode statusCode):this(content, request)
        {
            StatusCode = statusCode;
        }

        public PlainTextResult(string content, HttpRequestMessage request)
        {
            _request = request;
            Content = content;
            StatusCode = HttpStatusCode.OK;
        }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage()
            {
                Content = new StringContent(Content, Encoding.UTF8, CoreConstants.TextContentType),
                RequestMessage = _request,
                StatusCode = this.StatusCode
            };
            return Task.FromResult(response);
        }
    }
}
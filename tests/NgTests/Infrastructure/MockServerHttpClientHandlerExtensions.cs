// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Persistence;

namespace NgTests.Infrastructure
{
    public static class MockServerHttpClientHandlerExtensions
    {
        public static async Task AddStorage(this MockServerHttpClientHandler handler, IStorage storage)
        {
            var files = (await storage.List(CancellationToken.None)).Select(x => x.Uri);

            foreach (var file in files)
            {
                var storageFileUrl = file;
                var relativeFileUrl = "/" + storageFileUrl.ToString().Replace(storage.BaseAddress.ToString(), string.Empty);

                handler.SetAction(relativeFileUrl, async message =>
                {
                    var content = await storage.Load(storageFileUrl, CancellationToken.None);

                    var response = new HttpResponseMessage(HttpStatusCode.OK);

                    if (!string.IsNullOrEmpty(content.CacheControl))
                    {
                        response.Headers.CacheControl = CacheControlHeaderValue.Parse(content.CacheControl);
                    }
                    response.Content = new StreamContent(content.GetContentStream());

                    if (!string.IsNullOrEmpty(content.ContentType))
                    {
                        response.Content.Headers.ContentType = new MediaTypeHeaderValue(content.ContentType);
                    }

                    return response;
                });
            }
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Catalog
{
    public class TestDataHttpMessageHandler : HttpMessageHandler
    {
        private static readonly Dictionary<string, Func<string>> UrlToGetContent = new Dictionary<string, Func<string>>
        {
            { TestData.CatalogIndexUrl, () => TestData.CatalogIndex },
            { TestData.CatalogPageUrl, () => TestData.CatalogPage },
            { TestData.PackageDeleteCatalogLeafUrl, () => TestData.PackageDeleteCatalogLeaf },
            { TestData.PackageDetailsCatalogLeafUrl, () => TestData.PackageDetailsCatalogLeaf },
            { TestData.RegistrationIndexInlinedItemsUrl, () => TestData.RegistrationIndexInlinedItems },
            { TestData.RegistrationIndexWithoutInlinedItemsUrl, () => TestData.RegistrationIndexWithoutInlinedItems },
            { TestData.RegistrationLeafUnlistedUrl, () => TestData.RegistrationLeafUnlisted },
            { TestData.RegistrationLeafListedUrl, () => TestData.RegistrationLeafListed },
            { TestData.RegistrationPageUrl, () => TestData.RegistrationPage },
            { TestData.CatalogLeafInvalidDependencyVersionRangeUrl, () => TestData.CatalogLeafInvalidDependencyVersionRange },
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(request));
        }

        private HttpResponseMessage Send(HttpRequestMessage request)
        {
            Func<string> getContent;
            if (request.Method != HttpMethod.Get
                || !UrlToGetContent.TryGetValue(request.RequestUri.AbsoluteUri, out getContent))
            {
                return new HttpResponseMessage
                {
                    RequestMessage = request,
                    StatusCode = HttpStatusCode.NotFound,
                    Content = new StringContent(string.Empty),
                };
            }

            return new HttpResponseMessage
            {
                RequestMessage = request,
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(getContent()),
            };
        }
    }
}

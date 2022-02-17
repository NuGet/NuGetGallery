// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    /// <summary>
    /// This is a workaround for this problem:
    /// https://github.com/Azure/azure-sdk-for-net/issues/26197
    /// 
    /// For it to work, the provided index must have a null (unset similarity value). This allows the Azure Search
    /// REST API to pick the default similarity based on the API version.
    /// 2020-06-30 and later defaults to BM25.
    /// 2019-05-06 and earlier defaults to classic.
    /// </summary>
    public class ClassicSimilarityHandler : DelegatingHandler
    {
        private const string ExpectedPathAndQuery = "/indexes?api-version=2020-06-30";
        private const string QueryReplacement = "api-version=2019-05-06";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Only patch the API version query parameter on the POST request to create an index. Leave everything else
            // untouched.
            if (request.Method == HttpMethod.Post
                && request.RequestUri.PathAndQuery == ExpectedPathAndQuery)
            {
                var uriBuilder = new UriBuilder(request.RequestUri);
                uriBuilder.Query = QueryReplacement;
                request.RequestUri = uriBuilder.Uri;
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}

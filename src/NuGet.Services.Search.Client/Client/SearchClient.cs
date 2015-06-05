// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Client;
using NuGet.Services.Search.Models;

namespace NuGet.Services.Search.Client
{
    public class SearchClient
    {
        private readonly RetryingHttpClientWrapper _retryingHttpClientWrapper;
        private readonly ServiceDiscoveryClient _discoveryClient;
        private readonly string _resourceType;

        /// <summary>
        /// Create a search service client from the specified base uri and credentials.
        /// </summary>
        /// <param name="baseUri">The URL to the root of the service</param>
        /// <param name="handlers">Handlers to apply to the request in order from first to last</param>
        public SearchClient(Uri baseUri, params DelegatingHandler[] handlers)
            : this(baseUri, "SearchGalleryQueryService/3.0.0-rc", null, new BaseUrlHealthIndicatorStore(new NullHealthIndicatorLogger()), handlers)
        {
        }

        /// <summary>
        /// Create a search service client from the specified base uri and credentials.
        /// </summary>
        /// <param name="baseUri">The URL to the root of the service</param>
        /// <param name="resourceType">Resource type to query against</param>
        /// <param name="credentials">The credentials to connect to the service with</param>
        /// <param name="healthIndicatorStore">Health indicator store</param>
        /// <param name="handlers">Handlers to apply to the request in order from first to last</param>
        public SearchClient(Uri baseUri, string resourceType, ICredentials credentials, IEndpointHealthIndicatorStore healthIndicatorStore, params DelegatingHandler[] handlers)
        {
            _resourceType = resourceType;

            // Link the handlers
            HttpMessageHandler handler = new HttpClientHandler()
            {
                Credentials = credentials,
                AllowAutoRedirect = true,
                UseDefaultCredentials = credentials == null
            };

            foreach (var providedHandler in handlers.Reverse())
            {
                providedHandler.InnerHandler = handler;
                handler = providedHandler;
            }

            var httpClient = new HttpClient(handler, disposeHandler: true);

            _retryingHttpClientWrapper = new RetryingHttpClientWrapper(httpClient, healthIndicatorStore);
            _discoveryClient = new ServiceDiscoveryClient(httpClient, baseUri);
        }

        private static readonly Dictionary<SortOrder, string> SortNames = new Dictionary<SortOrder, string>
        {
            {SortOrder.LastEdited, "lastEdited"},
            {SortOrder.Relevance, "relevance"},
            {SortOrder.Published, "published"},
            {SortOrder.TitleAscending, "title-asc"},
            {SortOrder.TitleDescending, "title-desc"}
        };

        public async Task<ServiceResponse<SearchResults>> Search(
            string query,
            string projectTypeFilter = null,
            bool includePrerelease = false,
            string curatedFeed = null,
            SortOrder sortBy = SortOrder.Relevance,
            int skip = 0,
            int take = 10,
            bool isLuceneQuery = false,
            bool countOnly = false,
            bool explain = false,
            bool getAllVersions = false,
            string supportedFramework = null)
        {
            IDictionary<string, string> nameValue = new Dictionary<string, string>();
            nameValue.Add("q", query);
            nameValue.Add("skip", skip.ToString());
            nameValue.Add("take", take.ToString());
            nameValue.Add("sortBy", SortNames[sortBy]);

            if (!String.IsNullOrEmpty(supportedFramework))
            {
                nameValue.Add("supportedFramework", supportedFramework);
            }

            if (!String.IsNullOrEmpty(projectTypeFilter))
            {
                nameValue.Add("projectType", projectTypeFilter);
            }

            if (includePrerelease)
            {
                nameValue.Add("prerelease", "true");
            }

            if (!String.IsNullOrEmpty(curatedFeed))
            {
                nameValue.Add("feed", curatedFeed);
            }

            if (!isLuceneQuery)
            {
                nameValue.Add("luceneQuery", "false");
            }

            if (explain)
            {
                nameValue.Add("explanation", "true");
            }

            if (getAllVersions)
            {
                nameValue.Add("ignoreFilter", "true");
            }

            if (countOnly)
            {
                nameValue.Add("countOnly", "true");
            }

            var qs = new FormUrlEncodedContent(nameValue);
            var queryString = await qs.ReadAsStringAsync();

            var endpoints = await _discoveryClient.GetEndpointsForResourceType(_resourceType);
            var requestEndpoints = endpoints.Select(e => AppendPathToUri(e, "search/query", queryString));

            var httpResponseMessage = await _retryingHttpClientWrapper.GetAsync(requestEndpoints);
            return new ServiceResponse<SearchResults>(httpResponseMessage);
        }

        private static Uri AppendPathToUri(Uri uri, string pathToAppend, string queryString = null)
        {
            var builder = new UriBuilder(uri);
            builder.Path = builder.Path.TrimEnd('/') + "/" + pathToAppend.TrimStart('/');
            if (!string.IsNullOrEmpty(queryString))
            {
                builder.Query = queryString;
            }
            return builder.Uri;
        }

        public async Task<ServiceResponse<IDictionary<int, int>>> GetChecksums(int minKey, int maxKey)
        {
            var endpoints = await _discoveryClient.GetEndpointsForResourceType(_resourceType);
            var requestEndpoints = endpoints.Select(e => AppendPathToUri(e, "search/range", string.Format("min={0}&max={1}", minKey, maxKey)));

            var response = await _retryingHttpClientWrapper.GetAsync(requestEndpoints);
            return new ServiceResponse<IDictionary<int, int>>(
                response,
                async () => (await response.Content.ReadAsAsync<IDictionary<string, int>>())
                    .Select(pair => new KeyValuePair<int, int>(Int32.Parse(pair.Key), pair.Value))
                    .ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        public async Task<ServiceResponse<IEnumerable<string>>> GetStoredFieldNames()
        {
            var endpoints = await _discoveryClient.GetEndpointsForResourceType(_resourceType);
            var requestEndpoints = endpoints.Select(e => AppendPathToUri(e, "search/fields"));

            return new ServiceResponse<IEnumerable<string>>(
                await _retryingHttpClientWrapper.GetAsync(requestEndpoints));
        }

        public async Task<ServiceResponse<JObject>> GetDiagnostics()
        {
            var endpoints = await _discoveryClient.GetEndpointsForResourceType(_resourceType);
            var requestEndpoints = endpoints.Select(e => AppendPathToUri(e, "search/diag"));

            return new ServiceResponse<JObject>(
                await _retryingHttpClientWrapper.GetAsync(requestEndpoints));
        }
    }
}

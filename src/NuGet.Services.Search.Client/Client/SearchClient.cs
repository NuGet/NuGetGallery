// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using NuGet.Services.Client;
using NuGet.Services.Search.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Search.Client
{
    public class SearchClient
    {
        private readonly Task<ServiceDiscovery> _discoveryClient;
        private readonly string _resourceType;

        /// <summary>
        /// Create a search service client from the specified base uri and credentials.
        /// </summary>
        /// <param name="baseUri">The URL to the root of the service</param>
        /// <param name="handlers">Handlers to apply to the request in order from first to last</param>
        public SearchClient(Uri baseUri, params DelegatingHandler[] handlers)
            : this(baseUri, "SearchGalleryQueryService/3.0.0-rc", null, handlers)
        {
        }

        /// <summary>
        /// Create a search service client from the specified base uri and credentials.
        /// </summary>
        /// <param name="baseUri">The URL to the root of the service</param>
        /// <param name="credentials">The credentials to connect to the service with</param>
        /// <param name="handlers">Handlers to apply to the request in order from first to last</param>
        public SearchClient(Uri baseUri, string resourceType, ICredentials credentials, params DelegatingHandler[] handlers)
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

            var client = new HttpClient(handler, disposeHandler: true);

            _discoveryClient = ServiceDiscovery.Connect(client, baseUri);
        }

        /// <summary>
        /// Create a search service client from the specified HttpClient. This client MUST have a valid
        /// BaseAddress, as the WorkClient will always use relative URLs to request work service APIs.
        /// The BaseAddress should point at the root of the service, NOT at the work service node.
        /// </summary>
        /// <param name="client">The client to use</param>
        public SearchClient(HttpClient client)
        {
            throw new InvalidOperationException("Hopefully this isn't used, like ever.");
        }


        private static readonly Dictionary<SortOrder, string> _sortNames = new Dictionary<SortOrder, string>
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
            nameValue.Add("sortBy", _sortNames[sortBy]);

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

            FormUrlEncodedContent qs = new FormUrlEncodedContent(nameValue);

            var client = await _discoveryClient;

            var queryString = await qs.ReadAsStringAsync();
            var httpResponseMessage = await client.GetAsync(_resourceType, string.Format("search/query?{0}", queryString));
            return new ServiceResponse<SearchResults>(httpResponseMessage);
        }

        public async Task<ServiceResponse<IDictionary<int, int>>> GetChecksums(int minKey, int maxKey)
        {
            var client = await _discoveryClient;

            var response = await client.GetAsync(_resourceType, string.Format("search/range?min={0}&max={1}", minKey, maxKey));
            return new ServiceResponse<IDictionary<int, int>>(
                response,
                async () => (await response.Content.ReadAsAsync<IDictionary<string, int>>())
                    .Select(pair => new KeyValuePair<int, int>(Int32.Parse(pair.Key), pair.Value))
                    .ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        public async Task<ServiceResponse<IEnumerable<string>>> GetStoredFieldNames()
        {
            var client = await _discoveryClient;

            return new ServiceResponse<IEnumerable<string>>(
                await client.GetAsync(_resourceType, "search/fields"));
        }

        public async Task<ServiceResponse<JObject>> GetDiagnostics()
        {
            var client = await _discoveryClient;

            return new ServiceResponse<JObject>(
                await client.GetAsync(_resourceType, "search/diag"));
        }
    }
}

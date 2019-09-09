﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SearchModels = NuGetGallery.Infrastructure.Search.Models;

namespace NuGetGallery.Infrastructure.Search 
{
    public class GallerySearchClient : ISearchClient
    {
        private readonly string _searchPath = "search/query";
        private readonly string _diagnosticsPath = "search/diag";
        private readonly IResilientSearchClient _httpClient;

        /// <summary>
        /// Create a search service client.
        /// </summary>
        /// <param name="httpClient">The <see cref="HttpClient"/> to be used for the requests.</param>
        public GallerySearchClient(IResilientSearchClient resilientHttpClient) 
        {
            _httpClient = resilientHttpClient ?? throw new ArgumentNullException(nameof(resilientHttpClient));
        }

        public async Task<ServiceResponse<JObject>> GetDiagnostics()
        {
            return new ServiceResponse<JObject>(await _httpClient.GetAsync(_diagnosticsPath, null));
        }

        // This code is copied from the SearchClient 
        private static readonly Dictionary<SearchModels.SortOrder, string> SortNames = new Dictionary<SearchModels.SortOrder, string>
        {
            {SearchModels.SortOrder.LastEdited, "lastEdited"},
            {SearchModels.SortOrder.Relevance, "relevance"},
            {SearchModels.SortOrder.Published, "published"},
            {SearchModels.SortOrder.TitleAscending, "title-asc"},
            {SearchModels.SortOrder.TitleDescending, "title-desc"},
            {SearchModels.SortOrder.CreatedAscending, "created-asc"},
            {SearchModels.SortOrder.CreatedDescending, "created-desc"},
        };

        // This code is copied from the SearchClient 
        public async Task<ServiceResponse<SearchModels.SearchResults>> Search(
            string query,
            string projectTypeFilter = null,
            bool includePrerelease = false,
            SearchModels.SortOrder sortBy = SearchModels.SortOrder.Relevance,
            int skip = 0,
            int take = 10,
            bool isLuceneQuery = false,
            bool countOnly = false,
            bool explain = false,
            bool getAllVersions = false,
            string supportedFramework = null,
            string semVerLevel = null)
        {
            IDictionary<string, string> nameValue = new Dictionary<string, string>();
            nameValue.Add("q", query);
            nameValue.Add("skip", skip.ToString());
            nameValue.Add("take", take.ToString());
            nameValue.Add("sortBy", SortNames[sortBy]);

            if (!String.IsNullOrEmpty(semVerLevel))
            {
                nameValue.Add("semVerLevel", semVerLevel);
            }

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

            var httpResponseMessage = await _httpClient.GetAsync(_searchPath, queryString);
            return new ServiceResponse<SearchModels.SearchResults>(httpResponseMessage);
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Search;

namespace NuGetGallery
{
    public class AutocompleteServiceQuery
    {
        private readonly string _autocompletePath = "autocomplete";
        private readonly IResilientSearchClient _resilientSearchClient;

        public AutocompleteServiceQuery(IAppConfiguration configuration, IResilientSearchClient resilientSearchClient)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            _resilientSearchClient = resilientSearchClient;
        }

        public async Task<IReadOnlyList<string>> RunServiceQuery(
            string queryString, 
            bool? includePrerelease,
            bool? includeTestData,
            string semVerLevel = null)
        {
            queryString = BuildQueryString(queryString, includePrerelease, includeTestData, semVerLevel);
            var result = await ExecuteQuery(queryString);
            var resultObject = JObject.Parse(result);

            return resultObject["data"].Select(entry => entry.ToString()).ToList();
        }

        internal async Task<string> ExecuteQuery(string queryString)
        {
            var response = await _resilientSearchClient.GetAsync(_autocompletePath, queryString.TrimStart('?'));
            return await response.Content.ReadAsStringAsync();
        }

        internal string BuildQueryString(
            string queryString,
            bool? includePrerelease,
            bool? includeTestData,
            string semVerLevel = null)
        {
            queryString += $"&prerelease={includePrerelease ?? false}";

            if (includeTestData == true)
            {
                queryString += "&testData=true";
            }

            if (!string.IsNullOrEmpty(semVerLevel) && NuGetVersion.TryParse(semVerLevel, out _))
            {
                queryString += $"&semVerLevel={semVerLevel}";
            }

            return "?" + queryString.TrimStart('&');
        }
    }
}
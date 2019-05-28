// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using NuGetGallery.Configuration;
using NuGetGallery.Infrastructure.Search;
using NuGetGallery.Services.Telemetry;

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

        public async Task<IEnumerable<string>> RunServiceQuery(
            string queryString, 
            bool? includePrerelease,
            string semVerLevel = null)
        {
            queryString = BuildQueryString(queryString, includePrerelease, semVerLevel);
            var result = await ExecuteQuery(queryString);
            var resultObject = JObject.Parse(result);

            return resultObject["data"].Select(entry => entry.ToString());
        }

        internal async Task<string> ExecuteQuery(string queryString)
        {
            var response = await _resilientSearchClient.GetAsync(_autocompletePath, queryString.TrimStart('?'));
            return await response.Content.ReadAsStringAsync();
        }

        internal string BuildQueryString(string queryString, bool? includePrerelease, string semVerLevel = null)
        {
            queryString += $"&prerelease={includePrerelease ?? false}";

            NuGetVersion semVerLevelVersion;
            if (!string.IsNullOrEmpty(semVerLevel) && NuGetVersion.TryParse(semVerLevel, out semVerLevelVersion))
            {
                queryString += $"&semVerLevel={semVerLevel}";
            }

            return "?" + queryString.TrimStart('&');
        }
    }
}
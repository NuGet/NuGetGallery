// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Search.Client;
using NuGet.Versioning;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class AutoCompleteServiceQuery
    {
        private readonly ServiceDiscoveryClient _serviceDiscoveryClient;
        private readonly string _autocompleteServiceResourceType;
        private readonly RetryingHttpClientWrapper _httpClientToDeprecate;
        private readonly AutoCompleteSearchClient _autocompleteSearchClient;

        public AutoCompleteServiceQuery(IAppConfiguration configuration, AutoCompleteSearchClient autocompleteSearchClient)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _serviceDiscoveryClient = new ServiceDiscoveryClient(configuration.ServiceDiscoveryUri);
            _autocompleteServiceResourceType = configuration.AutocompleteServiceResourceType;
            _httpClientToDeprecate = new RetryingHttpClientWrapper(new HttpClient(), QuietLog.LogHandledException);
            _autocompleteSearchClient = autocompleteSearchClient;
        }

        public async Task<IEnumerable<string>> RunServiceQuery(
            string queryString, 
            bool? includePrerelease,
            string semVerLevel = null)
        {
            queryString = BuildQueryString(queryString, includePrerelease, semVerLevel);
            var result = await DeprecatedExecuteQuery(queryString);
            var resultObject = JObject.Parse(result);

            return resultObject["data"].Select(entry => entry.ToString());
        }

        private async Task<string> DeprecatedExecuteQuery(string queryString)
        {
            var endpoints = await _serviceDiscoveryClient.GetEndpointsForResourceType(_autocompleteServiceResourceType);
            endpoints = endpoints.Select(e => new Uri(e + queryString)).AsEnumerable();

            return await _httpClientToDeprecate.GetStringAsync(endpoints);
        }

        private async Task<string> ExecuteQuery(string queryString)
        {
            return await _autocompleteSearchClient.GetStringAsync(queryString);
        }

        internal string BuildQueryString(string queryString, bool? includePrerelease, string semVerLevel = null)
        {
            queryString += $"&prerelease={includePrerelease ?? false}";

            NuGetVersion semVerLevelVersion;
            if (!string.IsNullOrEmpty(semVerLevel) && NuGetVersion.TryParse(semVerLevel, out semVerLevelVersion))
            {
                queryString += $"&semVerLevel={semVerLevel}";
            }

            if (string.IsNullOrEmpty(queryString))
            {
                return string.Empty;
            }

            return "?" + queryString.TrimStart('&');
        }
    }
}
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Services.Search.Client;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public class AutoCompleteServiceQuery
    {
        private readonly ServiceDiscoveryClient _serviceDiscoveryClient;
        private readonly string _autocompleteServiceResourceType;
        private readonly RetryingHttpClientWrapper _httpClient;

        public AutoCompleteServiceQuery(IAppConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _serviceDiscoveryClient = new ServiceDiscoveryClient(configuration.ServiceDiscoveryUri);
            _autocompleteServiceResourceType = configuration.AutocompleteServiceResourceType;
            _httpClient = new RetryingHttpClientWrapper(new HttpClient());
        }

        public async Task<IEnumerable<string>> RunServiceQuery(
            string queryString, 
            bool? includePrerelease,
            string semVerLevel = null)
        {
            queryString += $"&prerelease={includePrerelease ?? false}";

            if (!string.IsNullOrEmpty(semVerLevel))
            {
                queryString += $"&semVerLevel={semVerLevel}";
            }

            var endpoints = await _serviceDiscoveryClient.GetEndpointsForResourceType(_autocompleteServiceResourceType);
            endpoints = endpoints.Select(e => new Uri(e + "?" + queryString)).AsEnumerable();

            var result = await _httpClient.GetStringAsync(endpoints);
            var resultObject = JObject.Parse(result);

            return resultObject["data"].Select(entry => entry.ToString());
        }
    }
}
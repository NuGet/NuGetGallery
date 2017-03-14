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
            _serviceDiscoveryClient = new ServiceDiscoveryClient(configuration.ServiceDiscoveryUri);
            _autocompleteServiceResourceType = configuration.AutocompleteServiceResourceType;
            _httpClient = new RetryingHttpClientWrapper(new HttpClient());
        }

        public async Task<IEnumerable<string>> RunQuery(string queryString, bool? includePrerelease)
        {
            queryString += $"&prerelease={includePrerelease ?? false}";
            var endpoints = await _serviceDiscoveryClient.GetEndpointsForResourceType(_autocompleteServiceResourceType);
            endpoints = endpoints.Select(e => new Uri(e + "?" + queryString)).AsEnumerable();

            var result = await _httpClient.GetStringAsync(endpoints);
            var resultObject = JObject.Parse(result);

            return resultObject["data"].Select(entry => entry.ToString());
        }
    }
}
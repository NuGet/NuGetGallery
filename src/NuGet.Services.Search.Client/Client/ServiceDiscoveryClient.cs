// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Services.Search.Client
{
    public class ServiceDiscoveryClient
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _serviceDiscoveryEndpoint;

        private ServiceIndexDocument _serviceIndexDocument;

        private readonly TimeSpan _serviceIndexDocumentExpiration = TimeSpan.FromMinutes(5);
        private readonly object _serviceIndexDocumentLock = new object();
        private bool _serviceIndexDocumentUpdating;

        public ServiceDiscoveryClient(Uri serviceDiscoveryEndpoint)
            : this(new HttpClient(), serviceDiscoveryEndpoint)
        {
        }

        public ServiceDiscoveryClient(HttpClient httpClient, Uri serviceDiscoveryEndpoint)
        {
            _httpClient = httpClient;
            _serviceDiscoveryEndpoint = serviceDiscoveryEndpoint;
        }

        public async Task<IEnumerable<Uri>> GetEndpointsForResourceType(string resourceType)
        {
            await DiscoverEndpointsAsync();

            return _serviceIndexDocument.Doc["resources"].Where(j => (j["@type"].Type == JTokenType.Array ? j["@type"].Any(v => (string)v == resourceType) : ((string)j["@type"]) == resourceType)).Select(o => o["@id"].ToObject<Uri>()).ToList();
        }

        public async Task DiscoverEndpointsAsync()
        {
            // Get out quick if we don't have anything to do.
            if (_serviceIndexDocument != null && (_serviceIndexDocumentUpdating || DateTime.UtcNow <= _serviceIndexDocument.UpdateTime + _serviceIndexDocumentExpiration))
            {
                return;
            }

            // Lock to make sure that we can only attempt one update at a time.
            lock (_serviceIndexDocumentLock)
            {
                if (_serviceIndexDocumentUpdating)
                    return;

                _serviceIndexDocumentUpdating = true;
            }

            // Fetch the service index document
            await _httpClient.GetStringAsync(_serviceDiscoveryEndpoint)
                .ContinueWith(t =>
                {
                    try
                    {
                        JObject serviceIndexDocument = JObject.Parse(t.Result);
                        _serviceIndexDocument = new ServiceIndexDocument(serviceIndexDocument, DateTime.UtcNow);
                    }
                    finally
                    {
                        _serviceIndexDocumentUpdating = false;
                    }
                });
        }

        class ServiceIndexDocument
        {
            public JObject Doc { get; private set; }
            public DateTime UpdateTime { get; private set; }

            public ServiceIndexDocument(JObject doc, DateTime updateTime)
            {
                Doc = doc;
                UpdateTime = updateTime;
            }
        }
    }
}
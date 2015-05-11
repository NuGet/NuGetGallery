// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Services.Search.Client
{
    // Let's not use NuGet's HttpClient for this.
    using HttpClient = System.Net.Http.HttpClient;

    public class ServiceDiscovery
    {
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

        private readonly TimeSpan _serviceIndexDocumentExpiration = TimeSpan.FromMinutes(5);
        private readonly Uri _serviceIndexUri;
        private ServiceIndexDocument _serviceIndexDocument;
        private readonly object _serviceIndexDocumentLock;
        private readonly HttpClient _httpClient;

        bool _serviceIndexDocumentUpdating;

        private ServiceDiscovery(HttpClient httpClient, Uri serviceIndexUri, JObject serviceIndexDocument)
        {
            _httpClient = httpClient;
            _serviceIndexUri = serviceIndexUri;
            _serviceIndexDocument = new ServiceIndexDocument(serviceIndexDocument, DateTime.UtcNow);
            _serviceIndexDocumentUpdating = false;
            _serviceIndexDocumentLock = new object();
        }

        public static async Task<ServiceDiscovery> Connect(HttpClient httpClient, Uri serviceIndexUri)
        {
            string serviceIndexDocString = await httpClient.GetStringAsync(serviceIndexUri);
            JObject serviceIndexDoc = JObject.Parse(serviceIndexDocString);

            return new ServiceDiscovery(httpClient, serviceIndexUri, serviceIndexDoc);
        }

        public IList<Uri> this[string type]
        {
            get
            {
                BeginUpdateServiceIndexDocument();
                return _serviceIndexDocument.Doc["resources"].Where(j => (j["@type"].Type == JTokenType.Array ? j["@type"].Any(v => (string)v == type) : ((string)j["@type"]) == type)).Select(o => o["@id"].ToObject<Uri>()).ToList();
            }
        }

        public async Task<T> GetAsyncByType<T>(Func<HttpClient, string, Task<T>> payload, string serviceType, string queryString)
        {
            IList<Uri> uris = this[serviceType];

            List<Exception> exceptions = new List<Exception>();

            // Try the whole list of endpoints twice each.
            for (int i = 0; i < 2; ++i)
            {
                foreach (Uri uri in uris)
                {
                    string loc = uri + queryString;
                    try
                    {
                        return await payload(_httpClient, loc);
                    }
                    catch (Exception ex)
                    {
                        // Accumulate a list of exceptions from failed requests. They'll only
                        // be thrown if no request succeeds against any endpoint.
                        exceptions.Add(ex);
                    }
                }
            }

            throw new AggregateException(exceptions);
        }

        public Task<string> GetStringAsync(string serviceType, string queryString)
        {
            return GetAsyncByType((h, u) => h.GetStringAsync(u), serviceType, queryString);
        }

        public Task<HttpResponseMessage> GetAsync(string serviceType, string queryString)
        {
            return GetAsyncByType((h, u) => h.GetAsync(u), serviceType, queryString);
        }

        void BeginUpdateServiceIndexDocument()
        {
            // Get out quick if we don't have anything to do.
            if (_serviceIndexDocumentUpdating || DateTime.UtcNow <= _serviceIndexDocument.UpdateTime + _serviceIndexDocumentExpiration)
                return;

            // Lock to make sure that we can only attempt one update at a time.
            lock (_serviceIndexDocumentLock)
            {
                if (_serviceIndexDocumentUpdating)
                    return;

                _serviceIndexDocumentUpdating = true;
            }

            _httpClient.GetStringAsync(_serviceIndexUri).ContinueWith(t =>
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
    }
}

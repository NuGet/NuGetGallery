// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Services.Metadata.Catalog.Helpers;

namespace NuGet.Jobs.RegistrationComparer
{
    public class HiveComparer
    {
        private readonly HttpClient _httpClient;
        private readonly JsonComparer _comparer;
        private readonly IOptionsSnapshot<RegistrationComparerConfiguration> _options;
        private readonly ILogger<HiveComparer> _logger;

        public HiveComparer(
            HttpClient httpClient,
            JsonComparer comparer,
            IOptionsSnapshot<RegistrationComparerConfiguration> options,
            ILogger<HiveComparer> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CompareAsync(
            IReadOnlyList<string> baseUrls,
            string id,
            IReadOnlyList<string> versions)
        {
            if (baseUrls.Count <= 1)
            {
                throw new ArgumentException("At least two base URLs must be provided.", nameof(baseUrls));
            }

            // Compare the indexes.
            var rawIndexes = await Task.WhenAll(baseUrls.Select(x => GetIndexAsync(x, id)));
            var areBothMissing = false;
            for (var i = 1; i < baseUrls.Count; i++)
            {
                if (AreBothMissing(
                   rawIndexes[i - 1].Url,
                   rawIndexes[i].Url,
                   rawIndexes[i - 1].Data,
                   rawIndexes[i].Data))
                {
                    areBothMissing = true;
                    continue;
                }

                var comparisonContext = new ComparisonContext(
                    id,
                    baseUrls[i - 1],
                    baseUrls[i],
                    rawIndexes[i - 1].Url,
                    rawIndexes[i].Url,
                    Normalizers.Index);

                _comparer.Compare(
                   rawIndexes[i - 1].Data,
                   rawIndexes[i].Data,
                   comparisonContext);
            }

            if (areBothMissing)
            {
                return;
            }

            // Deserialize the indexes so we can get the page URLs.
            var indexes = new List<DownloadedData<RegistrationIndex>>();
            foreach (var rawIndex in rawIndexes)
            {
                indexes.Add(new DownloadedData<RegistrationIndex>(
                    rawIndex.Url,
                    rawIndex.Data.ToObject<RegistrationIndex>(NuGetJsonSerialization.Serializer)));
            }

            // Download the pages (if any) and leaves.
            var pageUrlGroups = indexes
                .Select((x, i) => x
                    .Data
                    .Items
                    .Where(p => p.Items == null)
                    .Select(p => p.Url)
                    .ToList())
                .ToList();
            var leafUrlGroups = baseUrls
                .Select(x => versions.Select(v => $"{x}{id}/{v}.json").ToList())
                .ToList();

            var urls = new ConcurrentBag<string>(pageUrlGroups
                .SelectMany(x => x)
                .Concat(leafUrlGroups.SelectMany(x => x)));
            var urlToJson = new ConcurrentDictionary<string, JObject>();
            await ParallelAsync.Repeat(
                async () =>
                {
                    await Task.Yield();
                    while (urls.TryTake(out var pageUrl))
                    {
                        var json = await GetJObjectOrNullAsync(pageUrl);
                        urlToJson.TryAdd(pageUrl, json.Data);
                    }
                },
                _options.Value.MaxConcurrentPageAndLeafDownloadsPerId);

            // Compare the pages.
            for (var i = 1; i < baseUrls.Count; i++)
            {
                for (var pageIndex = 0; pageIndex < pageUrlGroups[i].Count; pageIndex++)
                {
                    var leftUrl = pageUrlGroups[i - 1][pageIndex];
                    var rightUrl = pageUrlGroups[i][pageIndex];

                    var comparisonContext = new ComparisonContext(
                        id,
                        baseUrls[i - 1],
                        baseUrls[i],
                        leftUrl,
                        rightUrl,
                        Normalizers.Page);

                    _comparer.Compare(
                       urlToJson[leftUrl],
                       urlToJson[rightUrl],
                       comparisonContext);
                }
            }

            // Compare the affected leaves.
            for (var i = 1; i < baseUrls.Count; i++)
            {
                for (var leafIndex = 0; leafIndex < leafUrlGroups[i].Count; leafIndex++)
                {
                    var leftUrl = leafUrlGroups[i - 1][leafIndex];
                    var rightUrl = leafUrlGroups[i][leafIndex];

                    try
                    {
                        if (AreBothMissing(
                            leftUrl,
                            rightUrl,
                            urlToJson[leftUrl],
                            urlToJson[rightUrl]))
                        {
                            continue;
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning(ex, "A comparison warning was found.");
                        continue;
                    }

                    var comparisonContext = new ComparisonContext(
                        id,
                        baseUrls[i - 1],
                        baseUrls[i],
                        leftUrl,
                        rightUrl,
                        Normalizers.Leaf);

                    _comparer.Compare(
                       urlToJson[leftUrl],
                       urlToJson[rightUrl],
                       comparisonContext);
                }
            }
        }

        private bool AreBothMissing(string leftUrl, string rightUrl, JObject left, JObject right)
        {
            if ((left == null) != (right == null))
            {
                throw new InvalidOperationException(Environment.NewLine +
                   $"One of the URLs exists, the other does not." + Environment.NewLine +
                   $"|  Left URL:     {leftUrl}" + Environment.NewLine +
                   $"|  Right URL:    {rightUrl}" + Environment.NewLine +
                   $"|  Left is 404:  {left == null}" + Environment.NewLine +
                   $"|  Right is 404: {right == null}" + Environment.NewLine);
            }

            return left == null;
        }

        private async Task<DownloadedData<JObject>> GetIndexAsync(string baseUrl, string id)
        {
            var url = $"{baseUrl}{id}/index.json";
            return await GetJObjectOrNullAsync(url);
        }

        private async Task<DownloadedData<JObject>> GetJObjectOrNullAsync(string url)
        {
            using (var response = await _httpClient.GetAsync(url))
            {
                _logger.LogInformation(
                    "Fetched {Url}: {StatusCode} {ReasonPhrase}",
                    url,
                    (int)response.StatusCode,
                    response.ReasonPhrase);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new DownloadedData<JObject>(url, null);
                }

                response.EnsureSuccessStatusCode();

                using (var stream = await _httpClient.GetStreamAsync(url))
                using (var streamReader = new StreamReader(stream))
                using (var jsonTextReader = new JsonTextReader(streamReader))
                {
                    jsonTextReader.DateParseHandling = DateParseHandling.None;

                    var data = JObject.Load(jsonTextReader);
                    return new DownloadedData<JObject>(url, data);
                }
            }
        }

        private class DownloadedData<T>
        {
            public DownloadedData(string url, T data)
            {
                Url = url;
                Data = data;
            }

            public string Url { get; }
            public T Data { get; }
        }
    }
}

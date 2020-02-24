// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
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
        private static long _requestId = 0;

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
            IReadOnlyList<HiveConfiguration> hives,
            string id,
            IReadOnlyList<string> versions)
        {
            if (hives.Count <= 1)
            {
                throw new ArgumentException("At least two hive configurations must be provided.", nameof(hives));
            }

            // Compare the indexes.
            var rawIndexes = await Task.WhenAll(hives.Select(x => GetIndexAsync(x, id)));
            var areBothMissing = false;
            for (var i = 1; i < hives.Count; i++)
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
                    hives[i - 1].BaseUrl,
                    hives[i].BaseUrl,
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
                    rawIndex.Hive,
                    rawIndex.Url,
                    rawIndex.StorageUrl,
                    rawIndex.Data.ToObject<RegistrationIndex>(NuGetJsonSerialization.Serializer)));
            }

            // Download the pages (if any) and leaves.
            var pageUrlGroups = indexes
                .Select((x, i) => x
                    .Data
                    .Items
                    .Where(p => p.Items == null)
                    .Select(p => new { x.Hive, p.Url })
                    .ToList())
                .ToList();
            var leafUrlGroups = hives
                .Select(x => versions
                    .Select(v => $"{x.BaseUrl}{id}/{v}.json")
                    .Select(u => new { Hive = x, Url = u })
                    .ToList())
                .ToList();

            var pairs = new ConcurrentBag<KeyValuePair<string, HiveConfiguration>>(pageUrlGroups
                .SelectMany(x => x)
                .Concat(leafUrlGroups.SelectMany(x => x))
                .Select(x => new KeyValuePair<string, HiveConfiguration>(x.Url, x.Hive)));
            var urlToJson = new ConcurrentDictionary<string, JObject>();
            await ParallelAsync.Repeat(
                async () =>
                {
                    await Task.Yield();
                    while (pairs.TryTake(out var pair))
                    {
                        var data = await GetJObjectOrNullAsync(pair.Value, pair.Key);
                        urlToJson.TryAdd(pair.Key, data.Data);
                    }
                },
                _options.Value.MaxConcurrentPageAndLeafDownloadsPerId);

            // Compare the pages.
            for (var i = 1; i < hives.Count; i++)
            {
                for (var pageIndex = 0; pageIndex < pageUrlGroups[i].Count; pageIndex++)
                {
                    var left = pageUrlGroups[i - 1][pageIndex];
                    var right = pageUrlGroups[i][pageIndex];

                    var comparisonContext = new ComparisonContext(
                        id,
                        hives[i - 1].BaseUrl,
                        hives[i].BaseUrl,
                        left.Url,
                        right.Url,
                        Normalizers.Page);

                    _comparer.Compare(
                       urlToJson[left.Url],
                       urlToJson[right.Url],
                       comparisonContext);
                }
            }

            // Compare the affected leaves.
            for (var i = 1; i < hives.Count; i++)
            {
                for (var leafIndex = 0; leafIndex < leafUrlGroups[i].Count; leafIndex++)
                {
                    var left = leafUrlGroups[i - 1][leafIndex];
                    var right = leafUrlGroups[i][leafIndex];

                    try
                    {
                        if (AreBothMissing(
                            left.Url,
                            right.Url,
                            urlToJson[left.Url],
                            urlToJson[right.Url]))
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
                        hives[i - 1].BaseUrl,
                        hives[i].BaseUrl,
                        left.Url,
                        right.Url,
                        Normalizers.Leaf);

                    _comparer.Compare(
                       urlToJson[left.Url],
                       urlToJson[right.Url],
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

        private async Task<DownloadedData<JObject>> GetIndexAsync(HiveConfiguration hive, string id)
        {
            var url = $"{hive.BaseUrl}{id}/index.json";
            return await GetJObjectOrNullAsync(hive, url);
        }

        private async Task<DownloadedData<JObject>> GetJObjectOrNullAsync(HiveConfiguration hive, string url)
        {
            if (!url.StartsWith(hive.BaseUrl))
            {
                throw new ArgumentException("The provided URL must start with the hive base URL.");
            }

            var storageUrl = hive.StorageBaseUrl + url.Substring(hive.BaseUrl.Length);
            var requestId = Interlocked.Increment(ref _requestId);
            _logger.LogInformation("[Request {RequestId}] Fetching {Url}", requestId, storageUrl);
            var stopwatch = Stopwatch.StartNew();
            using (var response = await _httpClient.GetAsync(storageUrl, HttpCompletionOption.ResponseContentRead))
            {
                _logger.LogInformation(
                    "[Request {RequestId}] Got {StatusCode} {ReasonPhrase} after {DurationMs}ms",
                    requestId,
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    (int)stopwatch.Elapsed.TotalMilliseconds);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return new DownloadedData<JObject>(hive, url, storageUrl, null);
                }

                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var streamReader = new StreamReader(stream))
                using (var jsonTextReader = new JsonTextReader(streamReader))
                {
                    jsonTextReader.DateParseHandling = DateParseHandling.None;

                    var data = JObject.Load(jsonTextReader);
                    return new DownloadedData<JObject>(hive, url, storageUrl, data);
                }
            }
        }

        private class DownloadedData<T>
        {
            public DownloadedData(HiveConfiguration hive, string url, string storageUrl, T data)
            {
                Hive = hive;
                Url = url;
                StorageUrl = storageUrl;
                Data = data;
            }

            public HiveConfiguration Hive { get; }
            public string Url { get; }
            public string StorageUrl { get; }
            public T Data { get; }
        }
    }
}

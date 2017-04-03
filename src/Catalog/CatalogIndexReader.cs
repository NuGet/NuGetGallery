// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

namespace NuGet.Services.Metadata.Catalog
{
    public class CatalogIndexReader
    {
        public const int MaxDegreeOfParallelism = 32;

        private readonly Uri _indexUri;
        private readonly CollectorHttpClient _httpClient;
        private JObject _context;

        public CatalogIndexReader(Uri indexUri, CollectorHttpClient httpClient)
        {
            _indexUri = indexUri;
            _httpClient = httpClient;
        }

        public async Task<IEnumerable<CatalogIndexEntry>> GetEntries()
        {
            JObject index = await _httpClient.GetJObjectAsync(_indexUri);

            // save the context used on the index
            JToken context = null;
            if (index.TryGetValue("@context", out context))
            {
                _context = context as JObject;
            }

            List<Tuple<DateTime, Uri>> pages = new List<Tuple<DateTime, Uri>>();

            foreach (var item in index["items"])
            {
                pages.Add(new Tuple<DateTime, Uri>(DateTime.Parse(item["commitTimeStamp"].ToString()), new Uri(item["@id"].ToString())));
            }

            return await GetEntriesAsync(pages.Select(p => p.Item2));
        }

        private async Task<ConcurrentBag<CatalogIndexEntry>> GetEntriesAsync(IEnumerable<Uri> pageUris)
        {
            var pageUriBag = new ConcurrentBag<Uri>(pageUris);
            var entries = new ConcurrentBag<CatalogIndexEntry>();
            var interner = new StringInterner();

            var tasks = Enumerable
                .Range(0, MaxDegreeOfParallelism)
                .Select(i => ProcessPageUris(pageUriBag, entries, interner))
                .ToList();

            await Task.WhenAll(tasks);

            return entries;
        }

        private async Task ProcessPageUris(ConcurrentBag<Uri> pageUriBag, ConcurrentBag<CatalogIndexEntry> entries, StringInterner interner)
        {
            await Task.Yield();
            Uri pageUri;
            while (pageUriBag.TryTake(out pageUri))
            {
                var json = await _httpClient.GetJObjectAsync(pageUri);

                foreach (var item in json["items"])
                {
                    // This string is unique.
                    var id = item["@id"].ToString();
                    
                    // These strings should be shared.
                    var type = interner.Intern(item["@type"].ToString());
                    var commitId = interner.Intern(item["commitId"].ToString());
                    var nugetId = interner.Intern(item["nuget:id"].ToString());
                    var nugetVersion = interner.Intern(item["nuget:version"].ToString());

                    // No string is directly operated on here.
                    var commitTimeStamp = item["commitTimeStamp"].ToObject<DateTime>();

                    var entry = new CatalogIndexEntry(
                        new Uri(id),
                        type,
                        commitId,
                        commitTimeStamp,
                        nugetId,
                        NuGetVersion.Parse(nugetVersion));

                    entries.Add(entry);
                }
            }
        }
    }
}

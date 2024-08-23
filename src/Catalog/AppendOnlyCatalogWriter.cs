// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.Metadata.Catalog.Persistence;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public class AppendOnlyCatalogWriter : CatalogWriterBase
    {
        private bool _first;

        public AppendOnlyCatalogWriter(
            IStorage storage,
            ITelemetryService telemetryService,
            CatalogContext context = null)
            : base(storage, telemetryService, context)
        {
            _first = true;
        }

        protected override Uri[] GetAdditionalRootType()
        {
            return new Uri[] { Schema.DataTypes.AppendOnlyCatalog, Schema.DataTypes.Permalink };
        }

        protected override async Task SaveRoot(
            Guid commitId,
            DateTime commitTimeStamp,
            IDictionary<string, CatalogItemSummary> pageEntries,
            IGraph commitMetadata,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            await base.SaveRoot(commitId, commitTimeStamp, pageEntries, commitMetadata, cancellationToken);
            _telemetryService.TrackCatalogIndexWriteDuration(stopwatch.Elapsed, RootUri);
        }

        protected override async Task<SavePagesResult> SavePages(Guid commitId, DateTime commitTimeStamp, IDictionary<string, CatalogItemSummary> itemEntries, CancellationToken cancellationToken)
        {
            IDictionary<string, CatalogItemSummary> pageEntries;
            if (_first && !Context.Append)
            {
                pageEntries = new Dictionary<string, CatalogItemSummary>();
                _first = false;
            }
            else
            {
                pageEntries = await LoadIndexResource(RootUri, cancellationToken);
            }

            Uri pageUri = GetPageUri(pageEntries, itemEntries.Count, out var isExistingPage, out var previousPageUri);

            var items = new Dictionary<string, CatalogItemSummary>(itemEntries);

            Uri finishedPageUri = null;
            if (isExistingPage)
            {
                IDictionary<string, CatalogItemSummary> existingItemEntries = await LoadIndexResource(pageUri, cancellationToken);
                foreach (var entry in existingItemEntries)
                {
                    items.Add(entry.Key, entry.Value);
                }
            }
            else
            {
                finishedPageUri = previousPageUri;
            }

            await SaveIndexResource(pageUri, Schema.DataTypes.CatalogPage, commitId, commitTimeStamp, items, RootUri, null, null, cancellationToken);

            pageEntries[pageUri.AbsoluteUri] = new CatalogItemSummary(Schema.DataTypes.CatalogPage, commitId, commitTimeStamp, items.Count);

            return new SavePagesResult(pageEntries, finishedPageUri);
        }

        private Uri GetPageUri(IDictionary<string, CatalogItemSummary> currentPageEntries, int newItemCount, out bool isExistingPage, out Uri previousPageUri)
        {
            (var maxPageNumber, var latestUri, var latestCount) = ExtractLatest(currentPageEntries);
            int nextPageNumber = maxPageNumber + 1;

            isExistingPage = false;
            previousPageUri = null;

            if (latestUri == null)
            {
                return CreatePageUri(Storage.BaseAddress, "page0");
            }

            previousPageUri = CreatePageUri(Storage.BaseAddress, string.Format("page{0}", maxPageNumber));

            if (latestCount + newItemCount > Context.MaxPageSize)
            {
                return CreatePageUri(Storage.BaseAddress, string.Format("page{0}", nextPageNumber));
            }

            isExistingPage = true;

            return latestUri;
        }

        private static Tuple<int, Uri, int> ExtractLatest(IDictionary<string, CatalogItemSummary> currentPageEntries)
        {
            int maxPageNumber = -1;
            Uri latestUri = null;
            int latestCount = 0;

            foreach (KeyValuePair<string, CatalogItemSummary> entry in currentPageEntries)
            {
                int first = entry.Key.IndexOf("page") + 4;
                int last = first;
                while (last < entry.Key.Length && char.IsNumber(entry.Key, last))
                {
                    last++;
                }
                string s = entry.Key.Substring(first, last - first);
                int pageNumber = int.Parse(s);

                if (pageNumber > maxPageNumber)
                {
                    maxPageNumber = pageNumber;
                    latestUri = new Uri(entry.Key);
                    latestCount = entry.Value.Count.Value;
                }
            }

            return new Tuple<int, Uri, int>(maxPageNumber, latestUri, latestCount);
        }
    }
}
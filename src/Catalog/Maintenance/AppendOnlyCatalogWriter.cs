using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class AppendOnlyCatalogWriter : CatalogWriterBase
    {
        bool _append;
        bool _first;

        public AppendOnlyCatalogWriter(Storage storage, CatalogContext context, int maxPageSize = 1000, bool append = true)
            : base(storage, context)
        {
            _append = append;
            _first = true;
            MaxPageSize = maxPageSize;
        }
        public int MaxPageSize
        {
            get;
            private set;
        }

        protected override async Task<IDictionary<string, CatalogItemSummary>> SavePages(Guid commitId, DateTime commitTimeStamp, IDictionary<string, CatalogItemSummary> itemEntries)
        {
            IDictionary<string, CatalogItemSummary> pageEntries;
            if (_first && !_append)
            {
                pageEntries = new Dictionary<string, CatalogItemSummary>();
                _first = false;
            }
            else
            {
                pageEntries = await LoadIndexResource(RootUri);
            }

            bool isExistingPage;
            Uri pageUri = GetPageUri(pageEntries, itemEntries.Count, out isExistingPage);

            if (isExistingPage)
            {
                IDictionary<string, CatalogItemSummary> existingItemEntries = await LoadIndexResource(pageUri);
                foreach (var entry in existingItemEntries)
                {
                    itemEntries.Add(entry);
                }
            }

            await SaveIndexResource(pageUri, Schema.DataTypes.CatalogPage, commitId, commitTimeStamp, itemEntries, null);

            pageEntries[pageUri.ToString()] = new CatalogItemSummary(Schema.DataTypes.CatalogPage, commitId, commitTimeStamp, itemEntries.Count);

            return pageEntries;
        }

        Uri GetPageUri(IDictionary<string, CatalogItemSummary> currentPageEntries, int newItemCount, out bool isExistingPage)
        {
            Tuple<int, Uri, int> latest = ExtractLatest(currentPageEntries);
            int nextPageNumber = latest.Item1 + 1;
            Uri latestUri = latest.Item2;
            int latestCount = latest.Item3;

            isExistingPage = false;

            if (latestUri == null)
            {
                return CreatePageUri(Storage.BaseAddress, "page0");
            }

            if (latestCount + newItemCount > MaxPageSize)
            {
                return CreatePageUri(Storage.BaseAddress, string.Format("page{0}", nextPageNumber));
            }

            isExistingPage = true;

            return latestUri;
        }

        protected virtual Uri CreatePageUri(Uri baseAddress, string relativeAddress)
        {
            if (GraphPersistence != null)
            {
                return GraphPersistence.CreatePageUri(baseAddress, relativeAddress);
            }
            else
            {
                return new Uri(baseAddress, relativeAddress + ".json");
            }
        }

        static Tuple<int, Uri, int> ExtractLatest(IDictionary<string, CatalogItemSummary> currentPageEntries)
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

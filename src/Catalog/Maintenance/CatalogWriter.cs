using Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VDS.RDF;

namespace Catalog.Maintenance
{
    public class CatalogWriter
    {
        Storage _storage;
        List<CatalogItem> _batch;
        CatalogContext _context;
        bool _append;

        public CatalogWriter(Storage storage, CatalogContext context, int maxPageSize = 1000, bool append = true)
        {
            Options.InternUris = false;

            _storage = storage;
            _context = context;
            _append = append;
            _batch = new List<CatalogItem>();
            MaxPageSize = maxPageSize;
        }

        public int MaxPageSize
        {
            get;
            private set;
        }

        public void Add(CatalogItem item)
        {
            _batch.Add(item);
        }

        public async Task Commit(DateTime timeStamp)
        {
            if (_batch.Count == 0)
            {
                return;
            }

            string baseAddress = string.Format("{0}{1}/", _storage.BaseAddress, _storage.Container);

            List<Uri> pageItems = new List<Uri>();
            List<Task> tasks = new List<Task>();
            foreach (CatalogItem item in _batch)
            {
                item.SetTimeStamp(timeStamp);
                item.SetBaseAddress(baseAddress);

                Uri resourceUri = new Uri(item.GetBaseAddress() + item.GetRelativeAddress());
                tasks.Add(_storage.Save("application/json", resourceUri, item.CreateContent(_context)));

                pageItems.Add(resourceUri);
            }

            await Task.WhenAll(tasks.ToArray());

            Uri rootResourceUri = new Uri(baseAddress + "catalog/index.json");

            string rootContent = null;
            if (_append)
            {
                rootContent = await _storage.Load(rootResourceUri);
            }

            CatalogRoot root = new CatalogRoot(rootResourceUri, rootContent);
            CatalogPage page;

            Tuple<Uri, int> latestPage = root.GetLatestPage();

            Uri pageResourceUri;
            if (latestPage == null || latestPage.Item2 + pageItems.Count > MaxPageSize)
            {
                pageResourceUri = root.AddNextPage(timeStamp, pageItems.Count);
                page = new CatalogPage(pageResourceUri, rootResourceUri);
            }
            else
            {
                pageResourceUri = latestPage.Item1;
                string pageContent = await _storage.Load(pageResourceUri);
                page = new CatalogPage(pageResourceUri, rootResourceUri, pageContent);
                root.UpdatePage(pageResourceUri, timeStamp, latestPage.Item2 + pageItems.Count);
            }

            foreach (Uri resourceUri in pageItems)
            {
                page.Add(resourceUri, timeStamp);
            }

            await _storage.Save("application/json", pageResourceUri, page.CreateContent(_context));

            await _storage.Save("application/json", rootResourceUri, root.CreateContent(_context));

            _batch.Clear();
        }
    }
}

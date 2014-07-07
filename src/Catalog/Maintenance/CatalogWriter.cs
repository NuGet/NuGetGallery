using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class CatalogWriter : IDisposable
    {
        Storage _storage;
        List<CatalogItem> _batch;
        CatalogContext _context;
        bool _append;
        bool _first;
        bool _open;
        
        public CatalogWriter(Storage storage, CatalogContext context, int maxPageSize = 1000, bool append = true)
        {
            Options.InternUris = false;

            _storage = storage;
            _context = context;
            _append = append;
            _batch = new List<CatalogItem>();
            MaxPageSize = maxPageSize;
            _first = true;
            _open = true;
        }

        public int MaxPageSize
        {
            get;
            private set;
        }

        public void Add(CatalogItem item)
        {
            Check();

            _batch.Add(item);
        }

        public Task Commit(IDictionary<string, string> commitUserData = null)
        {
            return Commit(DateTime.UtcNow, commitUserData);
        }

        public async Task Commit(DateTime timeStamp, IDictionary<string, string> commitUserData = null)
        {
            Guid commitId = Guid.NewGuid();

            Check();

            if (_batch.Count == 0)
            {
                return;
            }

            IDictionary<Uri, Tuple<Uri, IGraph>> pageItems = new Dictionary<Uri, Tuple<Uri, IGraph>>();
            List<Task> tasks = null;
            foreach (CatalogItem item in _batch)
            {
                item.SetTimeStamp(timeStamp);
                item.SetCommitId(commitId);
                item.SetBaseAddress(_storage.BaseAddress);

                Uri resourceUri = new Uri(item.GetBaseAddress(), item.GetRelativeAddress());
                StorageContent content = item.CreateContent(_context);
                IGraph pageContent = item.CreatePageContent(_context);

                if (content != null)
                {
                    if (tasks == null)
                    {
                        tasks = new List<Task>();
                    }

                    tasks.Add(_storage.Save(resourceUri, content));
                }

                pageItems.Add(resourceUri, new Tuple<Uri, IGraph>(item.GetItemType(), pageContent));
            }

            if (tasks != null)
            {
                await Task.WhenAll(tasks.ToArray());
            }

            Uri rootResourceUri = _storage.ResolveUri("index.json");

            string rootContent = null;
            if (!_first || _first && _append)
            {
                rootContent = await _storage.LoadString(rootResourceUri);
            }
            _first = false;

            CatalogRoot root = new CatalogRoot(rootResourceUri, rootContent);
            CatalogPage page;

            Tuple<Uri, int> latestPage = root.GetLatestPage();

            Uri pageResourceUri;
            if (latestPage == null || latestPage.Item2 + pageItems.Count > MaxPageSize)
            {
                pageResourceUri = root.AddNextPage(timeStamp, commitId, pageItems.Count);
                page = new CatalogPage(pageResourceUri, rootResourceUri);
            }
            else
            {
                pageResourceUri = latestPage.Item1;
                string pageContent = await _storage.LoadString(pageResourceUri);
                page = new CatalogPage(pageResourceUri, rootResourceUri, pageContent);
                root.UpdatePage(pageResourceUri, timeStamp, commitId, latestPage.Item2 + pageItems.Count);
            }

            foreach (KeyValuePair<Uri, Tuple<Uri, IGraph>> pageItem in pageItems)
            {
                page.Add(pageItem.Key, pageItem.Value.Item1, pageItem.Value.Item2, timeStamp, commitId);
            }

            page.SetTimeStamp(timeStamp);
            page.SetCommitId(commitId);

            await _storage.Save(pageResourceUri, page.CreateContent(_context));

            root.SetCommitUserData(commitUserData);
            root.SetTimeStamp(timeStamp);
            root.SetCommitId(commitId);

            await _storage.Save(rootResourceUri, root.CreateContent(_context));

            _batch.Clear();
        }
 
        public void Dispose()
        {
            _open = false;
        }

        public static async Task<IDictionary<string, string>> GetCommitUserData(Storage storage)
        {
            Uri rootResourceUri = GetRootResourceUri(storage);
            string content = await storage.LoadString(rootResourceUri);
            return CatalogRoot.GetCommitUserData(rootResourceUri, content);
        }

        public static async Task<DateTime> GetLastCommitTimeStamp(Storage storage)
        {
            Uri rootResourceUri = GetRootResourceUri(storage);
            string content = await storage.LoadString(rootResourceUri);
            return CatalogRoot.GetLastCommitTimeStamp(rootResourceUri, content);
        }

        public static async Task<int> GetCount(Storage storage)
        {
            Uri rootResourceUri = GetRootResourceUri(storage);
            string content = await storage.LoadString(rootResourceUri);
            return CatalogRoot.GetCount(rootResourceUri, content);
        }

        static Uri GetRootResourceUri(Storage storage)
        {
            return storage.ResolveUri("index.json");
        }

        void Check()
        {
            if (!_open)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace Catalog.Maintenance
{
    class CatalogRoot : CatalogContainer
    {
        IDictionary<Uri, Tuple<string, DateTime, int?>> _items;
        string _baseAddress;
        int _nextPageNumber;

        Uri _latestUri;
        int _latestCount;

        public CatalogRoot(Uri root, string content)
            : base(root)
        {
            _items = new Dictionary<Uri, Tuple<string, DateTime, int?>>();

            _nextPageNumber = 0;
            if (content != null)
            {
                Load(_items, content);

                Tuple<int, Uri, int> latest = ExtractLatest();
                _nextPageNumber = latest.Item1 + 1;
                _latestUri = latest.Item2;
                _latestCount = latest.Item3;
            }

            string s = root.ToString();
            _baseAddress = s.Substring(0, s.LastIndexOf('/') + 1);
        }

        public Uri AddNextPage(DateTime timeStamp, int count)
        {
            Uri nextPageAddress = new Uri(_baseAddress + string.Format("page{0}.json", _nextPageNumber++));
            _items.Add(nextPageAddress, new Tuple<string, DateTime, int?>("http://nuget.org/schema#Container", timeStamp, count));
            return nextPageAddress;
        }

        public Tuple<Uri, int> GetLatestPage()
        {
            return _latestUri == null ? null : new Tuple<Uri, int>(_latestUri, _latestCount);
        }

        public void UpdatePage(Uri pageUri, DateTime timeStamp, int count)
        {
            _items[pageUri] = new Tuple<string, DateTime, int?>("http://nuget.org/schema#Container", timeStamp, count);
        }

        protected override string GetContainerType()
        {
            return "http://nuget.org/schema#CatalogRoot";
        }

        protected override IDictionary<Uri, Tuple<string, DateTime, int?>> GetItems()
        {
            return _items;
        }

        Tuple<int, Uri, int> ExtractLatest()
        {
            int maxPageNumber = -1;
            Uri latestUri = null;
            int latestCount = 0;

            foreach (KeyValuePair<Uri, Tuple<string, DateTime, int?>> item in _items)
            {
                string s = item.Key.ToString();
                s = s.Substring(s.LastIndexOf('/') + 5);
                s = s.Substring(0, s.Length - 5);

                int pageNumber = int.Parse(s);
                if (pageNumber > maxPageNumber)
                {
                    maxPageNumber = pageNumber;
                    latestUri = item.Key;
                    latestCount = item.Value.Item3.Value;
                }
            }

            return new Tuple<int, Uri, int>(maxPageNumber, latestUri, latestCount);
        }
    }
}

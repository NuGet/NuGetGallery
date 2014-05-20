using System;
using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    class CatalogPage : CatalogContainer
    {
        IDictionary<Uri, Tuple<string, DateTime, int?>> _items;

        public CatalogPage(Uri page, Uri root, string content = null)
            : base(page, root)
        {
            _items = new Dictionary<Uri, Tuple<string, DateTime, int?>>();
            if (content != null)
            {
                Load(_items, content);
            }
        }

        public void Add(Uri resourceUri, string resourceType, DateTime timeStamp)
        {
            _items.Add(resourceUri, new Tuple<string, DateTime, int?>(resourceType, timeStamp, null));
        }

        protected override string GetContainerType()
        {
            return "http://nuget.org/catalog#Page";
        }

        protected override IDictionary<Uri, Tuple<string, DateTime, int?>> GetItems()
        {
            return _items;
        }
    }
}

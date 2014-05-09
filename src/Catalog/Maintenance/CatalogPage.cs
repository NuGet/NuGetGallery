using System;
using System.Collections.Generic;

namespace Catalog.Maintenance
{
    class CatalogPage : CatalogContainer
    {
        IDictionary<Uri, Tuple<DateTime, int?>> _items;

        public CatalogPage(Uri page, Uri root, string content = null)
            : base(page, root)
        {
            _items = new Dictionary<Uri, Tuple<DateTime, int?>>();
            if (content != null)
            {
                Load(_items, content);
            }
        }

        public void Add(Uri item, DateTime timeStamp)
        {
            _items.Add(item, new Tuple<DateTime, int?>(timeStamp, null));
        }

        protected override IDictionary<Uri, Tuple<DateTime, int?>> GetItems()
        {
            return _items;
        }
    }
}

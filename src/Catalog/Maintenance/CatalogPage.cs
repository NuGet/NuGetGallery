using System;
using System.Collections.Generic;

namespace Catalog.Maintenance
{
    public class CatalogPage : CatalogContainer
    {
        List<Tuple<Uri, DateTime, int?>> _items;

        public CatalogPage(Uri page, Uri root)
            : base(page, root)
        {
            _items = new List<Tuple<Uri, DateTime, int?>>();
        }

        public void Add(Uri item, DateTime timeStamp)
        {
            _items.Add(new Tuple<Uri, DateTime, int?>(item, timeStamp, null));
        }

        protected override IEnumerable<Tuple<Uri, DateTime, int?>> GetItems()
        {
            return _items;
        }
    }
}

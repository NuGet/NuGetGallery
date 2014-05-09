using System;
using System.Collections.Generic;

namespace Catalog.Maintenance
{
    class CatalogPage : CatalogContainer
    {
        List<Tuple<Uri, DateTime>> _items;

        public CatalogPage(Uri page, Uri root)
            : base(page, root)
        {
            _items = new List<Tuple<Uri, DateTime>>();
        }

        public void Add(Uri item, DateTime timeStamp)
        {
            _items.Add(new Tuple<Uri, DateTime>(item, timeStamp));
        }

        protected override IEnumerable<Tuple<Uri, DateTime>> GetItems()
        {
            return _items;
        }
    }
}

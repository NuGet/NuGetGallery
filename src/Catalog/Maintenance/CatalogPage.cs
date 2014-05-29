using System;
using System.Collections.Generic;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    class CatalogPage : CatalogContainer
    {
        IDictionary<Uri, Tuple<Uri, IGraph, DateTime, Guid, int?>> _items;

        public CatalogPage(Uri page, Uri root, string content = null)
            : base(page, root)
        {
            _items = new Dictionary<Uri, Tuple<Uri, IGraph, DateTime, Guid, int?>>();
            if (content != null)
            {
                Load(_items, content);
            }
        }

        public void Add(Uri resourceUri, Uri resourceType, IGraph pageContent, DateTime timeStamp, Guid commitId)
        {
            _items.Add(resourceUri, new Tuple<Uri, IGraph, DateTime, Guid, int?>(resourceType, pageContent, timeStamp, commitId, null));
        }

        protected override Uri GetContainerType()
        {
            return Constants.CatalogPage;
        }

        protected override IDictionary<Uri, Tuple<Uri, IGraph, DateTime, Guid, int?>> GetItems()
        {
            return _items;
        }
    }
}

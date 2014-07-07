using System;
using System.Collections.Generic;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    class CatalogPage : CatalogContainer
    {
        public CatalogPage(Uri page, Uri root, string content = null)
            : base(page, root)
        {
            if (content != null)
            {
                Load(_items, content);
            }
        }

        public void Add(Uri resourceUri, Uri resourceType, IGraph pageContent, DateTime timeStamp, Guid commitId)
        {
            _items.Add(resourceUri, new CatalogContainerItem
            {
                Type = resourceType,
                PageContent = pageContent,
                TimeStamp = timeStamp,
                CommitId = commitId
            });
        }

        protected override Uri GetContainerType()
        {
            return Schema.DataTypes.CatalogPage;
        }
    }
}

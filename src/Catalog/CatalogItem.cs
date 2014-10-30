using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class CatalogItem
    {
        public DateTime TimeStamp { get; set; }

        public Guid CommitId { get; set; }

        public Uri BaseAddress { get; set; }

        public abstract Uri GetItemType();

        public abstract Uri GetItemAddress();

        public virtual StorageContent CreateContent(CatalogContext context)
        {
            return null;
        }

        public virtual IGraph CreatePageContent(CatalogContext context)
        {
            return null;
        }

        /// <summary>
        /// Create the core graph used in CreateContent(context)
        /// </summary>
        public virtual IGraph CreateContentGraph(CatalogContext context)
        {
            return null;
        }
    }
}

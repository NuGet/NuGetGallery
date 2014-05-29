using System;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class CatalogContainerItem
    {
        public Uri Type { get; set; }
        public IGraph PageContent { get; set; }
        public DateTime TimeStamp { get; set; }
        public Guid CommitId { get; set; }
        public int? Count { get; set; }
    }
}

using System;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public class CatalogItemSummary
    {
        public CatalogItemSummary(Uri type, Guid commitId, DateTime commitTimeStamp, int? count = null, IGraph content = null)
        {
            Type = type;
            CommitId = commitId;
            CommitTimeStamp = commitTimeStamp;
            Count = count;
            Content = content;
        }

        public Uri Type { get; private set; }
        public Guid CommitId { get; private set; }
        public DateTime CommitTimeStamp { get; private set; }
        public int? Count { get; private set; }
        public IGraph Content { get; private set; }
    }
}

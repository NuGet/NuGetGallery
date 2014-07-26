using System;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public abstract class Entry
    {
        public abstract string Key { get; }
        public abstract IGraph GetSegmentContent(Uri uri);
    }
}

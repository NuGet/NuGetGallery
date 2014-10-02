using System;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public interface ICatalogGraphPersistence
    {
        Task SaveGraph(Uri resourceUri, IGraph graph, Uri typeUri);
        Task<IGraph> LoadGraph(Uri resourceUri);
        Uri CreatePageUri(Uri baseAddress, string relativeAddress);
    }
}

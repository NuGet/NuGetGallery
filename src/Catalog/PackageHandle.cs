using System;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;

namespace Catalog
{
    public abstract class PackageHandle : IInputDataHandle
    {
        public PackageHandle()
        {
        }

        public abstract Task<PackageData> GetData();

        public async Task<IGraph> CreateGraph(string baseAddress)
        {
            PackageData data = await GetData();

            string itemBaseAddress = baseAddress + "catalog/item/" + MakePublishedPathComponent(data.Published) + "/";

            IGraph graph = Utils.CreateNuspecGraph(data.Nuspec, itemBaseAddress);

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));

            Triple package = graph.GetTriplesWithPredicateObject(graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:Package")).First();

            graph.Assert(package.Subject, graph.CreateUriNode("nuget:published"), graph.CreateLiteralNode(data.Published.ToString(), new Uri("http://www.w3.org/2001/XMLSchema#dateTime")));

            Uri catalogUri = new Uri(baseAddress + "catalog/index.json");
            Uri catalogPageUri = new Uri(baseAddress + "catalog/page/" + data.RegistrationId.Substring(0, 1) + ".json");

            graph.Assert(graph.CreateUriNode(catalogUri), graph.CreateUriNode("nuget:item"), graph.CreateUriNode(catalogPageUri));
            graph.Assert(graph.CreateUriNode(catalogPageUri), graph.CreateUriNode("nuget:item"), package.Subject);

            graph.Assert(package.Subject, graph.CreateUriNode("nuget:container"), graph.CreateUriNode(catalogPageUri));
            graph.Assert(graph.CreateUriNode(catalogPageUri), graph.CreateUriNode("nuget:container"), graph.CreateUriNode(catalogUri));

            return graph;
        }

        string MakePublishedPathComponent(DateTime published)
        {
            return string.Format("{0}.{1}.{2}.{3}.{4}.{5}", published.Year, published.Month, published.Day, published.Hour, published.Minute, published.Second);
        }
    }
}

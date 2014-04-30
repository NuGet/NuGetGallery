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
            Uri catalogPageUri = new Uri(baseAddress + "catalog/page/" + MakePageName(data.RegistrationId) + ".json");

            graph.Assert(graph.CreateUriNode(catalogUri), graph.CreateUriNode("nuget:item"), graph.CreateUriNode(catalogPageUri));
            graph.Assert(graph.CreateUriNode(catalogPageUri), graph.CreateUriNode("nuget:item"), package.Subject);

            graph.Assert(package.Subject, graph.CreateUriNode("nuget:container"), graph.CreateUriNode(catalogPageUri));
            graph.Assert(graph.CreateUriNode(catalogPageUri), graph.CreateUriNode("nuget:container"), graph.CreateUriNode(catalogUri));

            graph.Assert(package.Subject, graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:Resource"));

            graph.Assert(graph.CreateUriNode(catalogUri), graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:Container"));
            graph.Assert(graph.CreateUriNode(catalogUri), graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:Resource"));

            graph.Assert(graph.CreateUriNode(catalogPageUri), graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:Container"));
            graph.Assert(graph.CreateUriNode(catalogPageUri), graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:Resource"));

            return graph;
        }

        string MakePublishedPathComponent(DateTime published)
        {
            return string.Format("{0}.{1}.{2}.{3}.{4}.{5}", published.Year, published.Month, published.Day, published.Hour, published.Minute, published.Second);
        }

        string MakePageName(string id)
        {
            if (id.Length < 2)
            {
                return id;
            }

            int take = GetTakeLengthForLetter(id[0]);
            take = Math.Min(take, id.Length);
            return id.Substring(0, take);
        }

        int GetTakeLengthForLetter(char ch)
        {
            switch (ch)
            {
                case 'a': return 4;
                case 'b': return 2;
                case 'c': return 2;
                case 'd': return 2;
                case 'e': return 2;
                case 'f': return 2;
                case 'g': return 2;
                case 'h': return 3;
                case 'i': return 3;
                case 'j': return 3;
                case 'k': return 1;
                case 'l': return 1;
                case 'm': return 5;
                case 'n': return 2;
                case 'o': return 3;
                case 'p': return 2;
                case 'q': return 1;
                case 'r': return 2;
                case 's': return 3;
                case 't': return 4;
                case 'u': return 1;
                case 'v': return 1;
                case 'w': return 2;
                case 'x': return 1;
                case 'y': return 1;
                case 'z': return 1;
            }
            return 1;
        }
    }
}

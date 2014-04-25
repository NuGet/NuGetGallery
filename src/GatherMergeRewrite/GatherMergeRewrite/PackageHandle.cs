using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using VDS.RDF;

namespace GatherMergeRewrite
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

            Uri ownerUri = new Uri(baseAddress + "owners/" + data.OwnerId + ".json");
            Uri registrationUri = new Uri(baseAddress + "packages/" + data.RegistrationId + ".json");

            IGraph graph = Utils.CreateNuspecGraph(data.Nuspec, baseAddress);

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));

            Triple triple = graph.GetTriplesWithPredicateObject(graph.CreateUriNode("rdf:type"), graph.CreateUriNode("nuget:Package")).First();

            graph.Assert(triple.Subject, graph.CreateUriNode("nuget:published"), graph.CreateLiteralNode(data.Published.ToString(), new Uri("http://www.w3.org/2001/XMLSchema#dateTime")));
            graph.Assert(graph.CreateUriNode(ownerUri), graph.CreateUriNode("nuget:owns"), graph.CreateUriNode(registrationUri));

            Uri catalogUri = new Uri(baseAddress + "catalog/index.json");
            Uri catalogPageUri = new Uri(baseAddress + "catalog/page/" + data.RegistrationId.Substring(0, 1) + ".json");
            Uri catalogRegistrationUri = new Uri(baseAddress + "catalog/registration/" + data.RegistrationId + ".json");

            graph.Assert(graph.CreateUriNode(catalogUri), graph.CreateUriNode("nuget:item"), graph.CreateUriNode(catalogPageUri));
            graph.Assert(graph.CreateUriNode(catalogPageUri), graph.CreateUriNode("nuget:item"), graph.CreateUriNode(catalogRegistrationUri));
            graph.Assert(graph.CreateUriNode(catalogRegistrationUri), graph.CreateUriNode("nuget:item"), triple.Subject);

            return graph;
        }
    }
}

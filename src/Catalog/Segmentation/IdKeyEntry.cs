using System;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Segmentation
{
    public class IdKeyEntry : Entry
    {
        public IdKeyEntry(string id, string version, string description, string registrationBaseAddress)
        {
            Id = id;
            Version = version;
            Description = description;
            RegistrationUri = new Uri(registrationBaseAddress.TrimEnd('/') + "/" + id + ".json");
        }

        public override string Key
        {
            get { return Id; }
        }

        public string Id { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }

        public Uri RegistrationUri { get; set; }

        public override IGraph GetSegmentContent(Uri uri)
        {
            IGraph graph = new Graph();

            graph.NamespaceMap.AddNamespace("nuget", new Uri("http://schema.nuget.org/schema#"));

            INode subject = graph.CreateUriNode(uri);

            graph.Assert(subject, graph.CreateUriNode("nuget:id"), graph.CreateLiteralNode(Id));
            graph.Assert(subject, graph.CreateUriNode("nuget:version"), graph.CreateLiteralNode(Version));
            graph.Assert(subject, graph.CreateUriNode("nuget:description"), graph.CreateLiteralNode(Description));
            graph.Assert(subject, graph.CreateUriNode("nuget:registration"), graph.CreateUriNode(RegistrationUri));

            return graph;
        }
    }
}

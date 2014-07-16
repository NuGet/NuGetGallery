using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class Segment
    {
        IList<SegmentEntry> _entries = new List<SegmentEntry>();
        public Uri Uri { get; set; }
        public IList<SegmentEntry> Entries { get { return _entries; } }
        public Segment()
        {
        }
        public Segment(IGraph graph)
        {
            FromGraph(graph);
        }
        public IGraph ToGraph()
        {
            IGraph graph = new Graph();

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("catalog", new Uri("http://schema.nuget.org/catalog#"));

            INode subject = graph.CreateUriNode(Uri);

            graph.Assert(subject, graph.CreateUriNode("rdf:type"), graph.CreateUriNode(new Uri("http://schema.nuget.org/catalog#Segment")));

            foreach (SegmentEntry entry in Entries)
            {
                graph.Assert(subject, graph.CreateUriNode("catalog:entry"), graph.CreateUriNode(entry.Uri));
                graph.Merge(entry.ToGraph(), true);
            }

            return graph;
        }
        void FromGraph(IGraph graph)
        {
            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("catalog", new Uri("http://schema.nuget.org/catalog#"));

            INode subject = graph.GetTriplesWithPredicateObject(graph.CreateUriNode("rdf:type"), graph.CreateUriNode("catalog:Segment")).First().Subject;

            foreach (Triple entry in graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode("catalog:entry")))
            {
                Entries.Add(new SegmentEntry(entry, graph));
            }

        }
        public class SegmentEntry : Entry
        {
            public SegmentEntry()
            {
            }

            public SegmentEntry(Triple entry, IGraph graph)
            {
                FromGraph(entry, graph);
            }

            public IGraph ToGraph()
            {
                IGraph graph = new Graph();

                graph.NamespaceMap.AddNamespace("nuget", new Uri("http://schema.nuget.org/schema#"));

                INode subject = graph.CreateUriNode(Uri);

                graph.Assert(subject, graph.CreateUriNode("nuget:id"), graph.CreateLiteralNode(Id));
                graph.Assert(subject, graph.CreateUriNode("nuget:version"), graph.CreateLiteralNode(Version));
                graph.Assert(subject, graph.CreateUriNode("nuget:description"), graph.CreateLiteralNode(Description));

                return graph;
            }

            void FromGraph(Triple entry, IGraph graph)
            {
                graph.NamespaceMap.AddNamespace("nuget", new Uri("http://schema.nuget.org/schema#"));

                Uri = ((IUriNode)entry.Object).Uri;

                Id = graph.GetTriplesWithSubjectPredicate(entry.Object, graph.CreateUriNode("nuget:id")).First().Object.ToString();
                Version = graph.GetTriplesWithSubjectPredicate(entry.Object, graph.CreateUriNode("nuget:version")).First().Object.ToString();
                Description = graph.GetTriplesWithSubjectPredicate(entry.Object, graph.CreateUriNode("nuget:description")).First().Object.ToString();
            }
        }
    }
}

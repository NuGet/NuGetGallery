using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Segmentation
{
    public class Segment
    {
        IList<SegmentEntry> _entries = new List<SegmentEntry>();
        public Uri Uri { get; set; }
        public IList<SegmentEntry> Entries { get { return _entries; } }
        
        public Segment(Uri uri)
        {
            Uri = uri;
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
                INode entryNode = graph.CreateUriNode(entry.Uri);

                graph.Assert(subject, graph.CreateUriNode("catalog:entry"), entryNode);
                graph.Assert(entryNode, graph.CreateUriNode("catalog:key"), graph.CreateLiteralNode(entry.Key));
                graph.Merge(entry.ToGraph(), true);
            }

            return graph;
        }

        void FromGraph(IGraph graph)
        {
            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("catalog", new Uri("http://schema.nuget.org/catalog#"));

            INode subject = graph.GetTriplesWithPredicateObject(graph.CreateUriNode("rdf:type"), graph.CreateUriNode("catalog:Segment")).First().Subject;

            Uri = ((IUriNode)subject).Uri;

            foreach (Triple entry in graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode("catalog:entry")))
            {
                Entries.Add(new SegmentEntry(((IUriNode)entry.Object).Uri, graph));
            }
        }
        
        public class SegmentEntry
        {
            IGraph _graph;

            public Uri Uri { get; set; }

            public string Key { get; set; }

            public SegmentEntry()
            {
            }

            public SegmentEntry(Uri subject, IGraph graph)
            {
                FromGraph(subject, graph);
            }

            public SegmentEntry(IGraph graph)
            {
                _graph = graph;
            }

            public IGraph ToGraph()
            {
                return _graph;
            }

            void FromGraph(Uri subjectUri, IGraph original)
            {
                _graph = new Graph();

                INode subject = original.CreateUriNode(subjectUri);

                foreach (Triple triple in original.GetTriplesWithSubject(subject))
                {
                    _graph.Assert(triple);
                }

                INode predicate = original.CreateUriNode("catalog:key");

                Key = ((ILiteralNode)original.GetTriplesWithSubjectPredicate(subject, predicate).First().Object).Value;
                Uri = subjectUri;
            }
        }
    }
}

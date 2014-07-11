using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class SegmentIndex
    {
        IList<SegmentSummary> _segments = new List<SegmentSummary>();
        public Uri Uri { get; private set; }
        public int SegmentNumber { get; private set; }
        public IList<SegmentSummary> Segments { get { return _segments; } }
        public SegmentIndex(Uri uri)
        {
            Uri = uri;
            SegmentNumber = 0;
        }

        public SegmentIndex(IGraph graph)
        {
            FromGraph(graph);
        }

        public string GetNextSegmentName()
        {
            return "segment_" + (SegmentNumber++).ToString() + ".json";
        }

        public IGraph ToGraph()
        {
            IGraph graph = new Graph();

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("catalog", new Uri("http://schema.nuget.org/catalog#"));

            INode subject = graph.CreateUriNode(Uri);

            graph.Assert(subject, graph.CreateUriNode("rdf:type"), graph.CreateUriNode("catalog:SegmentIndex"));

            graph.Assert(subject, graph.CreateUriNode("catalog:segmentNumber"), graph.CreateLiteralNode(SegmentNumber.ToString(), new Uri("http://www.w3.org/2001/XMLSchema#integer")));

            foreach (SegmentSummary entry in Segments)
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

            INode subject = graph.GetTriplesWithPredicateObject(graph.CreateUriNode("rdf:type"), graph.CreateUriNode("catalog:SegmentIndex")).First().Subject;

            Uri = ((IUriNode)subject).Uri;
            
            SegmentNumber = int.Parse(((ILiteralNode)graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode("catalog:segmentNumber")).First().Object).Value);

            foreach (Triple segment in graph.GetTriplesWithSubjectPredicate(subject, graph.CreateUriNode("catalog:entry")))
            {
                Segments.Add(new SegmentSummary(segment, graph));
            }
        }

        public class SegmentSummary
        {
            public Uri Uri { get; set; }
            public string Lowest { get; set; }
            public string Highest { get; set; }
            public int Count { get; set; }
            public SegmentSummary()
            {
            }
            public SegmentSummary(Triple segment, IGraph graph)
            {
                FromGraph(segment, graph);
            }
            public IGraph ToGraph()
            {
                IGraph graph = new Graph();

                graph.NamespaceMap.AddNamespace("catalog", new Uri("http://schema.nuget.org/catalog#"));

                INode subject = graph.CreateUriNode(Uri);

                graph.Assert(subject, graph.CreateUriNode("catalog:lowest"), graph.CreateLiteralNode(Lowest));
                graph.Assert(subject, graph.CreateUriNode("catalog:highest"), graph.CreateLiteralNode(Highest));
                graph.Assert(subject, graph.CreateUriNode("catalog:count"), graph.CreateLiteralNode(Count.ToString(), new Uri("http://www.w3.org/2001/XMLSchema#integer")));

                return graph;
            }

            void FromGraph(Triple segment, IGraph graph)
            {
                graph.NamespaceMap.AddNamespace("catalog", new Uri("http://schema.nuget.org/catalog#"));

                Uri = ((IUriNode)segment.Object).Uri;
                Lowest = graph.GetTriplesWithSubjectPredicate(segment.Object, graph.CreateUriNode("catalog:lowest")).First().Object.ToString();
                Highest = graph.GetTriplesWithSubjectPredicate(segment.Object, graph.CreateUriNode("catalog:highest")).First().Object.ToString();
                Count = int.Parse(((ILiteralNode)graph.GetTriplesWithSubjectPredicate(segment.Object, graph.CreateUriNode("catalog:count")).First().Object).Value);
            }
        }
    }
}

using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    abstract class CatalogContainer
    {
        Uri _resourceUri;
        Uri _parent;
        DateTime _timeStamp;
        Guid _commitId;

        public CatalogContainer(Uri resourceUri, Uri parent = null)
        {
            _resourceUri = resourceUri;
            _parent = parent;
        }

        public void SetTimeStamp(DateTime timeStamp)
        {
            _timeStamp = timeStamp;
        }

        public void SetCommitId(Guid commitId)
        {
            _commitId = commitId;
        }

        protected abstract IDictionary<Uri, Tuple<Uri, IGraph, DateTime, Guid, int?>> GetItems();

        protected abstract Uri GetContainerType();

        public StorageContent CreateContent(CatalogContext context)
        {
            IGraph graph = new Graph();

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("catalog", new Uri("http://nuget.org/catalog#"));

            INode rdfTypePredicate = graph.CreateUriNode("rdf:type");
            INode timeStampPredicate = graph.CreateUriNode("catalog:timeStamp");
            INode commitIdPredicate = graph.CreateUriNode("catalog:commitId");

            Uri dateTimeDatatype = new Uri("http://www.w3.org/2001/XMLSchema#dateTime");

            INode container = graph.CreateUriNode(_resourceUri);

            graph.Assert(container, rdfTypePredicate, graph.CreateUriNode(GetContainerType()));
            graph.Assert(container, timeStampPredicate, graph.CreateLiteralNode(_timeStamp.ToString(), dateTimeDatatype));
            graph.Assert(container, commitIdPredicate, graph.CreateLiteralNode(_commitId.ToString()));

            if (_parent != null)
            {
                graph.Assert(container, graph.CreateUriNode("catalog:parent"), graph.CreateUriNode(_parent));
            }

            AddCustomContent(container, graph);

            INode itemPredicate = graph.CreateUriNode("catalog:item");
            INode countPredicate = graph.CreateUriNode("catalog:count");

            foreach (KeyValuePair<Uri, Tuple<Uri, IGraph, DateTime, Guid, int?>> item in GetItems())
            {
                INode itemNode = graph.CreateUriNode(item.Key);

                graph.Assert(container, itemPredicate, itemNode);
                graph.Assert(itemNode, rdfTypePredicate, graph.CreateUriNode(item.Value.Item1));

                if (item.Value.Item2 != null)
                {
                    graph.Merge(item.Value.Item2);
                }

                graph.Assert(itemNode, timeStampPredicate, graph.CreateLiteralNode(item.Value.Item3.ToString(), dateTimeDatatype));
                graph.Assert(itemNode, commitIdPredicate, graph.CreateLiteralNode(item.Value.Item4.ToString()));

                if (item.Value.Item5 != null)
                {
                    Uri integerDatatype = new Uri("http://www.w3.org/2001/XMLSchema#integer");
                    graph.Assert(itemNode, countPredicate, graph.CreateLiteralNode(item.Value.Item5.ToString(), integerDatatype));
                }
            }

            JObject frame = context.GetJsonLdContext("context.Container.json", GetContainerType());

            StorageContent content = new StringStorageContent(Utils.CreateJson(graph, frame), "application/json");

            return content;
        }

        protected virtual void AddCustomContent(INode resource, IGraph graph)
        {
        }

        protected static void Load(IDictionary<Uri, Tuple<Uri, IGraph, DateTime, Guid, int?>> items, string content)
        {
            IGraph graph = Utils.CreateGraph(content);

            graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
            graph.NamespaceMap.AddNamespace("catalog", new Uri("http://nuget.org/catalog#"));
            INode rdfTypePredicate = graph.CreateUriNode("rdf:type");
            INode itemPredicate = graph.CreateUriNode("catalog:item");
            INode timeStampPredicate = graph.CreateUriNode("catalog:timeStamp");
            INode commitIdPredicate = graph.CreateUriNode("catalog:commitId");
            INode countPredicate = graph.CreateUriNode("catalog:count");

            foreach (Triple itemTriple in graph.GetTriplesWithPredicate(itemPredicate))
            {
                Uri itemUri = ((IUriNode)itemTriple.Object).Uri;

                Triple rdfTypeTriple = graph.GetTriplesWithSubjectPredicate(itemTriple.Object, rdfTypePredicate).First();
                Uri rdfType = ((IUriNode)rdfTypeTriple.Object).Uri;

                IGraph pageContent = null;
                INode pageContentSubjectNode = null;
                foreach (Triple pageContentTriple in graph.GetTriplesWithSubject(itemTriple.Object))
                {
                    if (pageContentTriple.Predicate.Equals(rdfTypePredicate))
                    {
                        continue;
                    }
                    if (pageContentTriple.Predicate.Equals(timeStampPredicate))
                    {
                        continue;
                    }
                    if (pageContentTriple.Predicate.Equals(commitIdPredicate))
                    {
                        continue;
                    }
                    if (pageContentTriple.Predicate.Equals(countPredicate))
                    {
                        continue;
                    }

                    if (pageContent == null)
                    {
                        pageContent = new Graph();
                        pageContentSubjectNode = pageContentTriple.Subject.CopyNode(pageContent, false);
                    }

                    INode pageContentPredicateNode = pageContentTriple.Predicate.CopyNode(pageContent, false);
                    INode pageContentObjectNode = pageContentTriple.Object.CopyNode(pageContent, false);

                    pageContent.Assert(pageContentSubjectNode, pageContentPredicateNode, pageContentObjectNode);
                }

                Triple timeStampTriple = graph.GetTriplesWithSubjectPredicate(itemTriple.Object, timeStampPredicate).First();
                DateTime timeStamp = DateTime.Parse(((ILiteralNode)timeStampTriple.Object).Value);

                Triple commitIdTriple = graph.GetTriplesWithSubjectPredicate(itemTriple.Object, commitIdPredicate).First();
                Guid commitId = Guid.Parse(((ILiteralNode)commitIdTriple.Object).Value);

                IEnumerable<Triple> countTriples = graph.GetTriplesWithSubjectPredicate(itemTriple.Object, countPredicate);

                int? count = null;
                if (countTriples.Count() > 0)
                {
                    Triple countTriple = countTriples.First();
                    count = int.Parse(((ILiteralNode)countTriple.Object).Value);
                }

                items.Add(itemUri, new Tuple<Uri, IGraph, DateTime, Guid, int?>(rdfType, pageContent, timeStamp, commitId, count));
            }
        }
    }
}

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

        protected IDictionary<Uri, CatalogContainerItem> _items;

        public CatalogContainer(Uri resourceUri, Uri parent = null)
        {
            _items = new Dictionary<Uri, CatalogContainerItem>();
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

        protected abstract Uri GetContainerType();

        public StorageContent CreateContent(CatalogContext context)
        {
            IGraph graph = new Graph();

            INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);
            INode timeStampPredicate = graph.CreateUriNode(Schema.Predicates.CatalogTimestamp);
            INode commitIdPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCommitId);

            INode container = graph.CreateUriNode(_resourceUri);

            graph.Assert(container, rdfTypePredicate, graph.CreateUriNode(GetContainerType()));
            graph.Assert(container, timeStampPredicate, graph.CreateLiteralNode(_timeStamp.ToString(), Schema.DataTypes.DateTime));
            graph.Assert(container, commitIdPredicate, graph.CreateLiteralNode(_commitId.ToString()));

            if (_parent != null)
            {
                graph.Assert(container, graph.CreateUriNode(Schema.Predicates.CatalogParent), graph.CreateUriNode(_parent));
            }

            AddCustomContent(container, graph);

            INode itemPredicate = graph.CreateUriNode(Schema.Predicates.CatalogItem);
            INode countPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCount);

            foreach (KeyValuePair<Uri, CatalogContainerItem> item in _items)
            {
                INode itemNode = graph.CreateUriNode(item.Key);

                graph.Assert(container, itemPredicate, itemNode);
                graph.Assert(itemNode, rdfTypePredicate, graph.CreateUriNode(item.Value.Type));

                if (item.Value.PageContent != null)
                {
                    graph.Merge(item.Value.PageContent);
                }

                graph.Assert(itemNode, timeStampPredicate, graph.CreateLiteralNode(item.Value.TimeStamp.ToString(), Schema.DataTypes.DateTime));
                graph.Assert(itemNode, commitIdPredicate, graph.CreateLiteralNode(item.Value.CommitId.ToString()));

                if (item.Value.Count != null)
                {
                    graph.Assert(itemNode, countPredicate, graph.CreateLiteralNode(item.Value.Count.ToString(), Schema.DataTypes.Integer));
                }
            }

            JObject frame = context.GetJsonLdContext("context.Container.json", GetContainerType());

            // The below code could be used to compact data storage by using relative URIs.
            //frame = (JObject)frame.DeepClone();
            //frame["@context"]["@base"] = _resourceUri.ToString();
            
            StorageContent content = new StringStorageContent(Utils.CreateJson(graph, frame), "application/json");

            return content;
        }

        protected virtual void AddCustomContent(INode resource, IGraph graph)
        {
        }

        protected static void Load(IDictionary<Uri, CatalogContainerItem> items, string content)
        {
            IGraph graph = Utils.CreateGraph(content);

            INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);
            INode itemPredicate = graph.CreateUriNode(Schema.Predicates.CatalogItem);
            INode timeStampPredicate = graph.CreateUriNode(Schema.Predicates.CatalogTimestamp);
            INode commitIdPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCommitId);
            INode countPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCount);

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

                items.Add(itemUri, new CatalogContainerItem
                {
                    Type = rdfType,
                    PageContent = pageContent,
                    TimeStamp = timeStamp,
                    CommitId = commitId, 
                    Count = count
                });
            }
        }
    }
}

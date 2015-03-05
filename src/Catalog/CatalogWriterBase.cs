using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class CatalogWriterBase : IDisposable
    {
        protected List<CatalogItem> _batch;
        protected bool _open;

        public CatalogWriterBase(IStorage storage, ICatalogGraphPersistence graphPersistence = null, CatalogContext context = null)
        {
            Options.InternUris = false;

            Storage = storage;
            GraphPersistence = graphPersistence;
            Context = context ?? new CatalogContext();

            _batch = new List<CatalogItem>();
            _open = true;

            RootUri = Storage.ResolveUri("index.json");
        }
        public void Dispose()
        {
            _batch.Clear();
            _open = false;
        }

        public IStorage Storage { get; private set; }

        public ICatalogGraphPersistence GraphPersistence { get; private set; }

        public Uri RootUri { get; private set; }

        public CatalogContext Context { get; private set; }

        public int Count { get { return _batch.Count; } }

        public void Add(CatalogItem item)
        {
            if (!_open)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            _batch.Add(item);
        }
        public Task Commit(IGraph commitMetadata = null)
        {
            return Commit(DateTime.UtcNow, commitMetadata);
        }

        public virtual async Task Commit(DateTime commitTimeStamp, IGraph commitMetadata = null)
        {
            if (!_open)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (_batch.Count == 0)
            {
                return;
            }

            //  the commitId is only used for tracing and trouble shooting

            Guid commitId = Guid.NewGuid();

            //  save items

            IDictionary<string, CatalogItemSummary> newItemEntries = await SaveItems(commitId, commitTimeStamp);

            //  save index pages - this is abstract as the derived class determines the index pagination

            IDictionary<string, CatalogItemSummary> pageEntries = await SavePages(commitId, commitTimeStamp, newItemEntries);

            //  save index root

            await SaveRoot(commitId, commitTimeStamp, pageEntries, commitMetadata);

            _batch.Clear();
        }

        async Task<IDictionary<string, CatalogItemSummary>> SaveItems(Guid commitId, DateTime commitTimeStamp)
        {
            ConcurrentDictionary<string, CatalogItemSummary> pageItems = new ConcurrentDictionary<string, CatalogItemSummary>();

            int batchIndex = 0;

            Task[] saveTasks = new Task[_batch.Count];

            foreach (CatalogItem item in _batch)
            {
                Uri resourceUri = null;

                try
                {
                    item.TimeStamp = commitTimeStamp;
                    item.CommitId = commitId;
                    item.BaseAddress = Storage.BaseAddress;

                    StorageContent content = item.CreateContent(Context);

                    resourceUri = item.GetItemAddress();

                    if (content != null)
                    {
                        saveTasks[batchIndex] = Storage.Save(resourceUri, content);
                    }

                    IGraph pageContent = item.CreatePageContent(Context);

                    if (!pageItems.TryAdd(resourceUri.AbsoluteUri, new CatalogItemSummary(item.GetItemType(), commitId, commitTimeStamp, null, pageContent)))
                    {
                        throw new Exception("Duplicate page: " + resourceUri.AbsoluteUri);
                    }

                    batchIndex++;
                }
                catch (Exception e)
                {
                    string msg = (resourceUri == null) ? string.Format("batch index: {0}", batchIndex) : string.Format("batch index: {0} resourceUri: {1}", batchIndex, resourceUri);
                    throw new Exception(msg, e);
                }
            }

            await Task.WhenAll(saveTasks);

            return pageItems;
        }

        async Task SaveRoot(Guid commitId, DateTime commitTimeStamp, IDictionary<string, CatalogItemSummary> pageEntries, IGraph commitMetadata)
        {
            await SaveIndexResource(RootUri, Schema.DataTypes.CatalogRoot, commitId, commitTimeStamp, pageEntries, null, commitMetadata, GetAdditionalRootType());
        }

        protected virtual Uri[] GetAdditionalRootType()
        {
            return null;
        }

        protected abstract Task<IDictionary<string, CatalogItemSummary>> SavePages(Guid commitId, DateTime commitTimeStamp, IDictionary<string, CatalogItemSummary> itemEntries);

        protected virtual StorageContent CreateIndexContent(IGraph graph, Uri type)
        {
            JObject frame = Context.GetJsonLdContext("context.Container.json", type);
            return new StringStorageContent(Utils.CreateJson(graph, frame), "application/json", "no-store");
        }

        protected async Task SaveIndexResource(Uri resourceUri, Uri typeUri, Guid commitId, DateTime commitTimeStamp, IDictionary<string, CatalogItemSummary> entries, Uri parent, IGraph extra = null, Uri[] additionalResourceTypes = null)
        {
            IGraph graph = new Graph();

            INode resourceNode = graph.CreateUriNode(resourceUri);
            INode itemPredicate = graph.CreateUriNode(Schema.Predicates.CatalogItem);
            INode typePredicate = graph.CreateUriNode(Schema.Predicates.Type);
            INode timeStampPredicate = graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp);
            INode commitIdPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCommitId);
            INode countPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCount);

            graph.Assert(resourceNode, typePredicate, graph.CreateUriNode(typeUri));
            graph.Assert(resourceNode, commitIdPredicate, graph.CreateLiteralNode(commitId.ToString()));
            graph.Assert(resourceNode, timeStampPredicate, graph.CreateLiteralNode(commitTimeStamp.ToString("O"), Schema.DataTypes.DateTime));
            graph.Assert(resourceNode, countPredicate, graph.CreateLiteralNode(entries.Count.ToString(), Schema.DataTypes.Integer));

            foreach (KeyValuePair<string, CatalogItemSummary> itemEntry in entries)
            {
                INode itemNode = graph.CreateUriNode(new Uri(itemEntry.Key));

                graph.Assert(resourceNode, itemPredicate, itemNode);
                graph.Assert(itemNode, typePredicate, graph.CreateUriNode(itemEntry.Value.Type));
                graph.Assert(itemNode, commitIdPredicate, graph.CreateLiteralNode(itemEntry.Value.CommitId.ToString()));
                graph.Assert(itemNode, timeStampPredicate, graph.CreateLiteralNode(itemEntry.Value.CommitTimeStamp.ToString("O"), Schema.DataTypes.DateTime));

                if (itemEntry.Value.Count != null)
                {
                    graph.Assert(itemNode, countPredicate, graph.CreateLiteralNode(itemEntry.Value.Count.ToString(), Schema.DataTypes.Integer));
                }

                if (itemEntry.Value.Content != null)
                {
                    graph.Merge(itemEntry.Value.Content, true);
                }
            }

            if (parent != null)
            {
                graph.Assert(resourceNode, graph.CreateUriNode(Schema.Predicates.CatalogParent), graph.CreateUriNode(parent));
            }

            if (extra != null)
            {
                graph.Merge(extra, true);
            }

            if (additionalResourceTypes != null)
            {
                foreach (Uri resourceType in additionalResourceTypes)
                {
                    graph.Assert(resourceNode, typePredicate, graph.CreateUriNode(resourceType));
                }
            }

            await SaveGraph(resourceUri, graph, typeUri);
        }

        protected async Task<IDictionary<string, CatalogItemSummary>> LoadIndexResource(Uri resourceUri)
        {
            IDictionary<string, CatalogItemSummary> entries = new Dictionary<string, CatalogItemSummary>();

            IGraph graph = await LoadGraph(resourceUri);

            if (graph == null)
            {
                return entries;
            }

            INode typePredicate = graph.CreateUriNode(Schema.Predicates.Type);
            INode itemPredicate = graph.CreateUriNode(Schema.Predicates.CatalogItem);
            INode timeStampPredicate = graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp);
            INode commitIdPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCommitId);
            INode countPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCount);

            CheckScheme(resourceUri, graph);

            INode resourceNode = graph.CreateUriNode(resourceUri);

            foreach (IUriNode itemNode in graph.GetTriplesWithSubjectPredicate(resourceNode, itemPredicate).Select((t) => t.Object))
            {
                Triple typeTriple = graph.GetTriplesWithSubjectPredicate(itemNode, typePredicate).First();
                Uri type = ((IUriNode)typeTriple.Object).Uri;

                Triple commitIdTriple = graph.GetTriplesWithSubjectPredicate(itemNode, commitIdPredicate).First();
                Guid commitId = Guid.Parse(((ILiteralNode)commitIdTriple.Object).Value);

                Triple timeStampTriple = graph.GetTriplesWithSubjectPredicate(itemNode, timeStampPredicate).First();
                DateTime timeStamp = DateTime.Parse(((ILiteralNode)timeStampTriple.Object).Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                Triple countTriple = graph.GetTriplesWithSubjectPredicate(itemNode, countPredicate).FirstOrDefault();
                int? count = (countTriple != null) ? int.Parse(((ILiteralNode)countTriple.Object).Value) : (int?)null;

                IGraph itemContent = null;
                INode itemContentSubjectNode = null;
                foreach (Triple itemContentTriple in graph.GetTriplesWithSubject(itemNode))
                {
                    if (itemContentTriple.Predicate.Equals(typePredicate))
                    {
                        continue;
                    }
                    if (itemContentTriple.Predicate.Equals(timeStampPredicate))
                    {
                        continue;
                    }
                    if (itemContentTriple.Predicate.Equals(commitIdPredicate))
                    {
                        continue;
                    }
                    if (itemContentTriple.Predicate.Equals(countPredicate))
                    {
                        continue;
                    }
                    if (itemContentTriple.Predicate.Equals(itemPredicate))
                    {
                        continue;
                    }

                    if (itemContent == null)
                    {
                        itemContent = new Graph();
                        itemContentSubjectNode = itemContentTriple.Subject.CopyNode(itemContent, false);
                    }

                    INode itemContentPredicateNode = itemContentTriple.Predicate.CopyNode(itemContent, false);
                    INode itemContentObjectNode = itemContentTriple.Object.CopyNode(itemContent, false);

                    itemContent.Assert(itemContentSubjectNode, itemContentPredicateNode, itemContentObjectNode);

                    if (itemContentTriple.Object is IUriNode)
                    {
                        Utils.CopyCatalogContentGraph(itemContentTriple.Object, graph, itemContent);
                    }
                }

                entries.Add(itemNode.Uri.AbsoluteUri, new CatalogItemSummary(type, commitId, timeStamp, count, itemContent));
            }

            return entries;
        }

        async Task SaveGraph(Uri resourceUri, IGraph graph, Uri typeUri)
        {
            if (GraphPersistence != null)
            {
                await GraphPersistence.SaveGraph(resourceUri, graph, typeUri);
            }
            else
            {
                await Storage.Save(resourceUri, CreateIndexContent(graph, typeUri));
            }
        }

        async Task<IGraph> LoadGraph(Uri resourceUri)
        {
            if (GraphPersistence != null)
            {
                return await GraphPersistence.LoadGraph(resourceUri);
            }
            else
            {
                return Utils.CreateGraph(await Storage.LoadString(resourceUri));
            }
        }

        protected Uri CreatePageUri(Uri baseAddress, string relativeAddress)
        {
            if (GraphPersistence != null)
            {
                return GraphPersistence.CreatePageUri(baseAddress, relativeAddress);
            }
            else
            {
                return new Uri(baseAddress, relativeAddress + ".json");
            }
        }

        void CheckScheme(Uri resourceUri, IGraph graph)
        {
            INode typePredicate = graph.CreateUriNode(Schema.Predicates.Type);

            Triple catalogRoot = graph.GetTriplesWithPredicateObject(typePredicate, graph.CreateUriNode(Schema.DataTypes.CatalogRoot)).FirstOrDefault();
            if (catalogRoot != null)
            {
                if (((UriNode)catalogRoot.Subject).Uri.Scheme != resourceUri.Scheme)
                {
                    throw new ArgumentException("the resource scheme does not match the existing catalog");
                }
            }
            Triple catalogPage = graph.GetTriplesWithPredicateObject(typePredicate, graph.CreateUriNode(Schema.DataTypes.CatalogPage)).FirstOrDefault();
            if (catalogPage != null)
            {
                if (((UriNode)catalogPage.Subject).Uri.Scheme != resourceUri.Scheme)
                {
                    throw new ArgumentException("the resource scheme does not match the existing catalog");
                }
            }
        }
    }
}

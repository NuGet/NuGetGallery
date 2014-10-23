using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class RegistrationCatalogWriter : CatalogWriterBase
    {
        IList<Uri> _cleanUpList;

        public RegistrationCatalogWriter(Storage storage, int partitionSize = 100, IList<Uri> cleanUpList = null, ICatalogGraphPersistence graphPersistence = null, CatalogContext context = null)
            : base(storage, graphPersistence, context)
        {
            _cleanUpList = cleanUpList;
            PartitionSize = partitionSize;
        }

        public int PartitionSize { get; private set; }

        protected override Uri[] GetAdditionalRootType()
        {
            return new Uri[] { Schema.DataTypes.PackageRegistration, Schema.DataTypes.Permalink };
        }

        protected override async Task<IDictionary<string, CatalogItemSummary>> SavePages(Guid commitId, DateTime commitTimeStamp, IDictionary<string, CatalogItemSummary> itemEntries)
        {
            SortedDictionary<NuGetVersion, KeyValuePair<string, CatalogItemSummary>> versions = new SortedDictionary<NuGetVersion, KeyValuePair<string, CatalogItemSummary>>();

            //  load items from existing pages

            IDictionary<string, CatalogItemSummary> pageEntries = await LoadIndexResource(RootUri);

            foreach (KeyValuePair<string, CatalogItemSummary> pageEntry in pageEntries)
            {
                IDictionary<string, CatalogItemSummary> pageItemEntries = await LoadIndexResource(new Uri(pageEntry.Key));

                foreach (KeyValuePair<string, CatalogItemSummary> pageItemEntry in pageItemEntries)
                {
                    NuGetVersion version = GetPackageVersion(new Uri(pageItemEntry.Key), pageItemEntry.Value.Content);
                    versions.Add(version, pageItemEntry);
                }
            }

            //  add new items

            foreach (KeyValuePair<string, CatalogItemSummary> itemEntry in itemEntries)
            {
                NuGetVersion version = GetPackageVersion(new Uri(itemEntry.Key), itemEntry.Value.Content);
                versions[version] = itemEntry;
            }

            //  (re)create pages

            IDictionary<string, CatalogItemSummary> newPageEntries = await PartitionAndSavePages(commitId, commitTimeStamp, versions);

            //  add to list of pages to clean up

            if (_cleanUpList != null)
            {
                foreach (string existingPage in pageEntries.Keys)
                {
                    if (!newPageEntries.ContainsKey(existingPage))
                    {
                        _cleanUpList.Add(new Uri(existingPage));
                    }
                }
            }

            return newPageEntries;
        }

        async Task<IDictionary<string, CatalogItemSummary>> PartitionAndSavePages(Guid commitId, DateTime commitTimeStamp, SortedDictionary<NuGetVersion, KeyValuePair<string, CatalogItemSummary>> versions)
        {
            IDictionary<string, CatalogItemSummary> newPageEntries = new Dictionary<string, CatalogItemSummary>();

            foreach (IEnumerable<KeyValuePair<NuGetVersion, KeyValuePair<string, CatalogItemSummary>>> partition in Utils.Partition(versions, PartitionSize))
            {
                string lower = partition.First().Key.ToString();
                string upper = partition.Last().Key.ToString();

                Uri newPageUri = CreatePageUri(Storage.BaseAddress, ("page/" + lower + "/" + upper).ToLowerInvariant());

                IDictionary<string, CatalogItemSummary> newPageItemEntries = new Dictionary<string, CatalogItemSummary>();
                foreach (KeyValuePair<NuGetVersion, KeyValuePair<string, CatalogItemSummary>> version in partition)
                {
                    newPageItemEntries.Add(version.Value);
                }

                IGraph extra = CreateExtraGraph(newPageUri, lower, upper);

                await SaveIndexResource(newPageUri, Schema.DataTypes.CatalogPage, commitId, commitTimeStamp, newPageItemEntries, RootUri, extra);

                newPageEntries[newPageUri.AbsoluteUri] = new CatalogItemSummary(Schema.DataTypes.CatalogPage, commitId, commitTimeStamp, newPageItemEntries.Count, CreatePageSummary(newPageUri, lower, upper));
            }

            return newPageEntries;
        }

        static IGraph CreateExtraGraph(Uri pageUri, string lower, string upper)
        {
            IGraph graph = new Graph();
            INode resourceNode = graph.CreateUriNode(pageUri);
            graph.Assert(resourceNode, graph.CreateUriNode(Schema.Predicates.Lower), graph.CreateLiteralNode(lower));
            graph.Assert(resourceNode, graph.CreateUriNode(Schema.Predicates.Upper), graph.CreateLiteralNode(upper));
            return graph;
        }

        static NuGetVersion GetPackageVersion(Uri packageUri, IGraph pageContent)
        {
            Triple t1 = pageContent.GetTriplesWithSubjectPredicate(
                pageContent.CreateUriNode(packageUri),
                pageContent.CreateUriNode(Schema.Predicates.CatalogEntry)).FirstOrDefault();

            Triple t2 = pageContent.GetTriplesWithSubjectPredicate(
                pageContent.CreateUriNode(((IUriNode)t1.Object).Uri),
                pageContent.CreateUriNode(Schema.Predicates.Version)).FirstOrDefault();

            string s = t2.Object.ToString();
            return NuGetVersion.Parse(s);
        }

        static IGraph CreatePageSummary(Uri newPageUri, string lower, string upper)
        {
            IGraph graph = new Graph();

            INode resourceUri = graph.CreateUriNode(newPageUri);

            graph.Assert(resourceUri, graph.CreateUriNode(Schema.Predicates.Lower), graph.CreateLiteralNode(lower));
            graph.Assert(resourceUri, graph.CreateUriNode(Schema.Predicates.Upper), graph.CreateLiteralNode(upper));

            return graph;
        }

        protected override StorageContent CreateIndexContent(IGraph graph, Uri type)
        {
            JObject frame = Context.GetJsonLdContext("context.Registration.json", type);
            return new StringStorageContent(Utils.CreateJson(graph, frame), "application/json", "no-store");
        }
    }
}

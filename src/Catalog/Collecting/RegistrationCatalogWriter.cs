using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class RegistrationCatalogWriter : CatalogWriterBase
    {
        IList<Uri> _cleanUpList;

        public RegistrationCatalogWriter(Storage storage, IList<Uri> cleanUpList)
            : base(storage)
        {
            _cleanUpList = cleanUpList;
        }

        protected override async Task<IDictionary<string, CatalogItemSummary>> SavePages(Guid commitId, DateTime commitTimeStamp, IDictionary<string, CatalogItemSummary> itemEntries)
        {
            SortedDictionary<NuGetVersion, KeyValuePair<string, CatalogItemSummary>> versions = new SortedDictionary<NuGetVersion, KeyValuePair<string, CatalogItemSummary>>();

            //  load all items from existing pages

            IDictionary<string, CatalogItemSummary> pageEntries = await LoadIndexResource(RootUri);

            foreach (KeyValuePair<string, CatalogItemSummary> pageEntry in pageEntries)
            {
                IDictionary<string, CatalogItemSummary> pageItemEntries = await LoadIndexResource(new Uri(pageEntry.Key));
                foreach (KeyValuePair<string, CatalogItemSummary> pageItemEntry in pageItemEntries)
                {
                    NuGetVersion version = GetVersion(pageItemEntry.Value.Content);
                    versions.Add(version, pageItemEntry);
                }
            }

            //  add new items

            foreach (KeyValuePair<string, CatalogItemSummary> itemEntry in itemEntries)
            {
                NuGetVersion version = GetVersion(itemEntry.Value.Content);
                versions.Add(version, itemEntry);
            }

            //  create page uri - let's just start with one page for now

            string lower = versions.First().Key.ToString();
            string upper = versions.Last().Key.ToString();

            Uri newPageUri = new Uri(Storage.BaseAddress, "page/" + lower + "/" + upper + ".json");

            IDictionary<string, CatalogItemSummary> newPageItemEntries = new Dictionary<string, CatalogItemSummary>();
            foreach (KeyValuePair<NuGetVersion, KeyValuePair<string, CatalogItemSummary>> version in versions)
            {
                newPageItemEntries.Add(version.Value);
            }

            IGraph extra = CreateExtraGraph(newPageUri, lower, upper);

            await SaveIndexResource(newPageUri, Schema.DataTypes.CatalogPage, commitId, commitTimeStamp, newPageItemEntries, extra);

            IDictionary<string, CatalogItemSummary> newPageEntries = new Dictionary<string, CatalogItemSummary>();

            newPageEntries[newPageUri.ToString()] = new CatalogItemSummary(Schema.DataTypes.CatalogPage, commitId, commitTimeStamp, newPageItemEntries.Count, CreatePageSummary(newPageUri, lower, upper));

            //  pages to clean up

            foreach (string existingPage in pageEntries.Keys)
            {
                if (!newPageEntries.ContainsKey(existingPage))
                {
                    _cleanUpList.Add(new Uri(existingPage));
                }
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

        static NuGetVersion GetVersion(IGraph pageContent)
        {
            Triple triple = pageContent.GetTriplesWithPredicate(pageContent.CreateUriNode(Schema.Predicates.Version)).FirstOrDefault();
            string s = triple.Object.ToString();
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

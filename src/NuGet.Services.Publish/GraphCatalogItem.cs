using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using VDS.RDF;

namespace NuGet.Services.Publish
{
    public class GraphCatalogItem : AppendOnlyCatalogItem
    {
        IDictionary<string, JObject> _metadata;
        Uri _nupkgAddress;
        long _packageSize;
        string _packageHash;
        DateTime _published;
        string _publisher;

        Guid _catalogItemId;
        JObject _context;

        public GraphCatalogItem(IDictionary<string, JObject> metadata, Uri nupkgAddress, long packageSize, string packageHash, DateTime published, string publisher)
        {
            _metadata = metadata;
            _nupkgAddress = nupkgAddress;
            _packageSize = packageSize;
            _packageHash = packageHash;
            _published = published;
            _publisher = publisher;

            _catalogItemId = Guid.NewGuid();

            _context = LoadContext("context.catalog.json");
            _context["@type"] = GetItemType().AbsoluteUri;
        }

        static JObject LoadContext(string name)
        {
            string assName = Assembly.GetExecutingAssembly().GetName().Name;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(assName + "." + name))
            {
                string json = new StreamReader(stream).ReadToEnd();
                return JObject.Parse(json);
            }
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.Package;
        }

        protected override string GetItemIdentity()
        {
            return _catalogItemId.ToString().ToLowerInvariant();
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            IGraph catalogEntry = new Graph();

            INode subject = catalogEntry.CreateUriNode(GetItemAddress());

            //  catalog infrastructure fields

            catalogEntry.Assert(subject, catalogEntry.CreateUriNode(Schema.Predicates.Type), catalogEntry.CreateUriNode(GetItemType()));
            catalogEntry.Assert(subject, catalogEntry.CreateUriNode(Schema.Predicates.CatalogTimeStamp), catalogEntry.CreateLiteralNode(TimeStamp.ToString("O"), Schema.DataTypes.DateTime));
            catalogEntry.Assert(subject, catalogEntry.CreateUriNode(Schema.Predicates.CatalogCommitId), catalogEntry.CreateLiteralNode(CommitId.ToString()));

            catalogEntry.Assert(subject, catalogEntry.CreateUriNode(Schema.Predicates.PackageContent), catalogEntry.CreateUriNode(_nupkgAddress));
            catalogEntry.Assert(subject, catalogEntry.CreateUriNode(Schema.Predicates.PackageSize), catalogEntry.CreateLiteralNode(_packageSize.ToString()));
            catalogEntry.Assert(subject, catalogEntry.CreateUriNode(Schema.Predicates.PackageHash), catalogEntry.CreateLiteralNode(_packageHash));
            catalogEntry.Assert(subject, catalogEntry.CreateUriNode(Schema.Predicates.Published), catalogEntry.CreateLiteralNode(_published.ToString("O"), Schema.DataTypes.DateTime));
            catalogEntry.Assert(subject, catalogEntry.CreateUriNode(Schema.Predicates.Publisher), catalogEntry.CreateLiteralNode(_publisher));

            //  add each metadata set

            foreach (KeyValuePair<string, JObject> item in _metadata)
            {
                Uri id = item.Value["@id"].ToObject<Uri>();

                IGraph graph = Utils.CreateGraph(item.Value);

                INode dataSet = graph.CreateUriNode(id);

                graph.Assert(dataSet, graph.CreateUriNode(Schema.Predicates.FileName), graph.CreateLiteralNode(item.Key));

                catalogEntry.Merge(graph, true);
                catalogEntry.Assert(subject, graph.CreateUriNode(Schema.Predicates.Details), dataSet);
            }

            //  create JSON content

            string json = Utils.CreateJson(catalogEntry, _context);

            StorageContent content = new StringStorageContent(json, "application/json", "no-store");

            return content;
        }
    }
}

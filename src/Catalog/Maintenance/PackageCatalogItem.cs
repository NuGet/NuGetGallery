using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public abstract class PackageCatalogItem : CatalogItem
    {
        string _id;
        string _version;

        protected abstract XDocument GetNuspec();

        public override StorageContent CreateContent(CatalogContext context)
        {
            XDocument original = GetNuspec();
            XDocument nuspec = NormalizeNuspecNamespace(original, context.GetXslt("xslt.normalizeNuspecNamespace.xslt"));
            IGraph graph = CreateNuspecGraph(nuspec, GetBaseAddress(), context.GetXslt("xslt.nuspec.xslt"));

            INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);
            INode timeStampPredicate = graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp);
            INode commitIdPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCommitId);
            INode publishedPredicate = graph.CreateUriNode(Schema.Predicates.Published);

            Triple resource = graph.GetTriplesWithPredicateObject(rdfTypePredicate, graph.CreateUriNode(GetItemType())).First();
            graph.Assert(resource.Subject, timeStampPredicate, graph.CreateLiteralNode(GetTimeStamp().ToString("O"), Schema.DataTypes.DateTime));
            graph.Assert(resource.Subject, commitIdPredicate, graph.CreateLiteralNode(GetCommitId().ToString()));
            graph.Assert(resource.Subject, publishedPredicate, graph.CreateLiteralNode(GetTimeStamp().ToString("O"), Schema.DataTypes.DateTime));

            INode idPredicate = graph.CreateUriNode(Schema.Predicates.Id);
            INode versionPredicate = graph.CreateUriNode(Schema.Predicates.Version);

            Triple id = graph.GetTriplesWithSubjectPredicate(resource.Subject, idPredicate).FirstOrDefault();
            if (id != null)
            {
                _id = ((ILiteralNode)id.Object).Value;
            }

            Triple version = graph.GetTriplesWithSubjectPredicate(resource.Subject, versionPredicate).FirstOrDefault();
            if (version != null)
            {
                _version = ((ILiteralNode)version.Object).Value;
            }

            JObject frame = context.GetJsonLdContext("context.Package.json", GetItemType());

            StorageContent content = new StringStorageContent(Utils.CreateJson(graph, frame), "application/json", "no-store");

            return content;
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.Package;
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            Uri resourceUri = new Uri(GetBaseAddress() + GetRelativeAddress());

            Graph graph = new Graph();

            INode subject = graph.CreateUriNode(resourceUri);
            INode idPredicate = graph.CreateUriNode(Schema.Predicates.Id);
            INode versionPredicate = graph.CreateUriNode(Schema.Predicates.Version);

            if (_id != null)
            {
                graph.Assert(subject, idPredicate, graph.CreateLiteralNode(_id));
            }

            if (_version != null)
            {
                graph.Assert(subject, versionPredicate, graph.CreateLiteralNode(_version));
            }

            return graph;
        }
        protected override string GetItemIdentity()
        {
            return (_id + "." + _version).ToLowerInvariant();
        }

        static XDocument NormalizeNuspecNamespace(XDocument original, XslCompiledTransform xslt)
        {
            XDocument result = new XDocument();
            using (XmlWriter writer = result.CreateWriter())
            {
                xslt.Transform(original.CreateReader(), writer);
            }
            return result;
        }

        static IGraph CreateNuspecGraph(XDocument nuspec, Uri baseAddress, XslCompiledTransform xslt)
        {
            XsltArgumentList arguments = new XsltArgumentList();
            arguments.AddParam("base", "", baseAddress.ToString());
            arguments.AddParam("extension", "", ".json");

            arguments.AddExtensionObject("urn:helper", new XsltHelper());

            XDocument rdfxml = new XDocument();
            using (XmlWriter writer = rdfxml.CreateWriter())
            {
                xslt.Transform(nuspec.CreateReader(), arguments, writer);
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(rdfxml.CreateReader());

            IGraph graph = new Graph();
            RdfXmlParser rdfXmlParser = new RdfXmlParser();
            rdfXmlParser.Load(graph, doc);

            return graph;
        }

        /// <summary>
        /// Ensure this item has been fully loaded if it was lazy loaded.
        /// </summary>
        public virtual void Load()
        {
            // get the nuspec and throw it away
            XDocument nuspec = GetNuspec();
            nuspec = null;
        }
    }
}

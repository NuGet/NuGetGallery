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
        protected abstract XDocument GetNuspec();

        public override StorageContent CreateContent(CatalogContext context)
        {
            XDocument original = GetNuspec();
            XDocument nuspec = NormalizeNuspecNamespace(original, context.GetXslt("xslt.normalizeNuspecNamespace.xslt"));
            IGraph graph = CreateNuspecGraph(nuspec, GetBaseAddress(), context.GetXslt("xslt.nuspec.xslt"));

            INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);
            INode timeStampPredicate = graph.CreateUriNode(Schema.Predicates.CatalogTimestamp);
            INode commitIdPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCommitId);
            Triple resource = graph.GetTriplesWithPredicateObject(rdfTypePredicate, graph.CreateUriNode(GetItemType())).First();
            graph.Assert(resource.Subject, timeStampPredicate, graph.CreateLiteralNode(GetTimeStamp().ToString(), Schema.DataTypes.DateTime));
            graph.Assert(resource.Subject, commitIdPredicate, graph.CreateLiteralNode(GetCommitId().ToString()));

            JObject frame = context.GetJsonLdContext("context.Package.json", GetItemType());

            StorageContent content = new StringStorageContent(Utils.CreateJson(graph, frame), "application/json");

            return content;
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.Package;
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
    }
}

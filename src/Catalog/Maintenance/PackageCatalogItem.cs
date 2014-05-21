using Newtonsoft.Json.Linq;
using System;
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

        public override string CreateContent(CatalogContext context)
        {
            XDocument original = GetNuspec();
            XDocument nuspec = NormalizeNuspecNamespace(original, context.GetXslt("xslt.normalizeNuspecNamespace.xslt"));
            IGraph graph = CreateNuspecGraph(nuspec, GetBaseAddress(), context.GetXslt("xslt.nuspec.xslt"));

            JObject frame = context.GetJsonLdContext("context.Package.json", GetItemType());

            string content = Utils.CreateJson(graph, frame);

            return content;
        }

        public override Uri GetItemType()
        {
            return Constants.Package;
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

        static IGraph CreateNuspecGraph(XDocument nuspec, string baseAddress, XslCompiledTransform xslt)
        {
            XsltArgumentList arguments = new XsltArgumentList();
            arguments.AddParam("base", "", baseAddress);
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

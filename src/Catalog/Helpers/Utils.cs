using NuGet.Services.Metadata.Catalog.JsonLDIntegration;
using JsonLD.Core;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NuGet.Services.Metadata.Catalog
{
    public class Utils
    {
        public static Stream GetResourceStream(string resName)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + "." + resName);
        }

        public static string GetResource(string resName)
        {
            return new StreamReader(GetResourceStream(resName)).ReadToEnd();
        }

        public static IGraph CreateNuspecGraph(XDocument nuspec, string baseAddress)
        {
            nuspec = NormalizeNuspecNamespace(nuspec);

            XslCompiledTransform transform = CreateTransform("xslt.nuspec.xslt");

            XsltArgumentList arguments = new XsltArgumentList();
            arguments.AddParam("base", "", baseAddress + "packages/");
            arguments.AddParam("extension", "", ".json");

            arguments.AddExtensionObject("urn:helper", new XsltHelper());

            XDocument rdfxml = new XDocument();
            using (XmlWriter writer = rdfxml.CreateWriter())
            {
                transform.Transform(nuspec.CreateReader(), arguments, writer);
            }

            RdfXmlParser rdfXmlParser = new RdfXmlParser();
            XmlDocument doc = new XmlDocument();
            doc.Load(rdfxml.CreateReader());
            IGraph graph = new Graph();
            rdfXmlParser.Load(graph, doc);

            return graph;
        }

        static XslCompiledTransform CreateTransform(string name)
        {
            XslCompiledTransform transform = new XslCompiledTransform();
            transform.Load(XmlReader.Create(new StreamReader(Utils.GetResourceStream(name))));
            return transform;
        }

        public static void Dump(IGraph graph, TextWriter writer)
        {
            CompressingTurtleWriter turtleWriter = new CompressingTurtleWriter();
            turtleWriter.DefaultNamespaces.AddNamespace("nuget", new Uri("http://nuget.org/schema#"));
            turtleWriter.DefaultNamespaces.AddNamespace("catalog", new Uri("http://nuget.org/catalog#"));
            turtleWriter.PrettyPrintMode = true;
            turtleWriter.CompressionLevel = 10;
            turtleWriter.Save(graph, writer);
        }

        public static void Dump(TripleStore store, TextWriter writer)
        {
            Dump(store.Graphs.First(), writer);
        }

        public static IGraph Load(string name)
        {
            TurtleParser parser = new TurtleParser();
            IGraph g = new Graph();
            parser.Load(g, new StreamReader(Utils.GetResourceStream(name)));
            return g;
        }

        public static XDocument GetNuspec(ZipArchive package)
        {
            if (package == null) { return null; }

            foreach (ZipArchiveEntry part in package.Entries)
            {
                if (part.FullName.EndsWith(".nuspec") && part.FullName.IndexOf('/') == -1)
                {
                    XDocument nuspec = XDocument.Load(part.Open());
                    return nuspec;
                }
            }
            return null;
        }

        public static ZipArchive GetPackage(Stream stream)
        {
            ZipArchive package = new ZipArchive(stream);
            return package;
        }

        public static XDocument NormalizeNuspecNamespace(XDocument original)
        {
            XDocument result = new XDocument();

            using (XmlWriter writer = result.CreateWriter())
            {
                XslCompiledTransform xslt = new XslCompiledTransform();
                xslt.Load(XmlReader.Create(new StreamReader(Utils.GetResourceStream("xslt.normalizeNuspecNamespace.xslt"))));
                xslt.Transform(original.CreateReader(), writer);
            }

            return result;
        }

        public static string CreateHtmlView(Uri resource, string frame, string baseAddress)
        {
            XDocument original = XDocument.Load(new StreamReader(Utils.GetResourceStream("html.view.html")));
            XslCompiledTransform transform = CreateTransform("xslt.view.xslt");
            XsltArgumentList arguments = new XsltArgumentList();
            arguments.AddParam("resource", "", resource.ToString());
            arguments.AddParam("frame", "", frame);
            arguments.AddParam("base", "", baseAddress);

            System.IO.StringWriter writer = new System.IO.StringWriter();
            using (XmlTextWriter xmlWriter = new XmlHtmlWriter(writer))
            {
                xmlWriter.Formatting = System.Xml.Formatting.Indented;
                transform.Transform(original.CreateReader(), arguments, xmlWriter);
            }

            return writer.ToString();
        }

        public static string CreateJson(IGraph graph, JToken frame = null)
        {
            System.IO.StringWriter writer = new System.IO.StringWriter();
            IRdfWriter rdfWriter = new JsonLdWriter();
            rdfWriter.Save(graph, writer);
            writer.Flush();

            if (frame == null)
            {
                return writer.ToString();
            }
            else
            {
                JToken flattened = JToken.Parse(writer.ToString());
                JObject framed = JsonLdProcessor.Frame(flattened, frame, new JsonLdOptions());
                JObject compacted = JsonLdProcessor.Compact(framed, frame["@context"], new JsonLdOptions());

                return compacted.ToString();
            }
        }

        public static IGraph CreateGraph(string json)
        {
            if (json == null)
            {
                return null;
            }

            JToken compacted = JToken.Parse(json);
            return CreateGraph(compacted);
        }

        public static IGraph CreateGraph(JToken compacted)
        {
            JToken flattened = JsonLdProcessor.Flatten(compacted, new JsonLdOptions());

            IRdfReader rdfReader = new JsonLdReader();
            IGraph graph = new Graph();
            rdfReader.Load(graph, new StringReader(flattened.ToString()));

            return graph;
        }

        public static bool IsCatalogNode(INode sourceNode, IGraph source)
        {
            Triple rootTriple = source.GetTriplesWithSubjectObject(sourceNode, source.CreateUriNode(Schema.DataTypes.CatalogRoot)).FirstOrDefault();
            Triple pageTriple = source.GetTriplesWithSubjectObject(sourceNode, source.CreateUriNode(Schema.DataTypes.CatalogPage)).FirstOrDefault();

            return (rootTriple != null || pageTriple != null);
        }

        public static void CopyCatalogContentGraph(INode sourceNode, IGraph source, IGraph target)
        {
            if (IsCatalogNode(sourceNode, source))
            {
                return;
            }

            foreach (Triple triple in source.GetTriplesWithSubject(sourceNode))
            {
                if (target.Assert(triple.CopyTriple(target)) && triple.Object is IUriNode)
                {
                    CopyCatalogContentGraph(triple.Object, source, target);
                }
            }
        }

        public static int CountItems(string indexJson)
        {
            if (indexJson == null)
            {
                return 0;
            }

            JObject index = JObject.Parse(indexJson);

            int total = 0;
            foreach (JObject item in index["items"])
            {
                total += item["count"].ToObject<int>();
            }

            return total;
        }

        public static bool IsType(JObject context, JObject obj, Uri type)
        {
            JToken objTypeToken;
            if (obj.TryGetValue("@type", out objTypeToken))
            {
                Uri objType = Expand(context, objTypeToken);
                return objType == type;
            }
            return false;
        }

        public static bool IsType(JObject context, JObject obj, Uri[] types)
        {
            foreach (Uri type in types)
            {
                if (Utils.IsType(context, obj, type))
                {
                    return true;
                }
            }
            return false;
        }

        public static Uri Expand(JObject context, JToken token)
        {
            string term = token.ToString();
            if (term.StartsWith("http:", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(term);
            }
            
            int indexOf = term.IndexOf(':');
            if (indexOf > 0)
            {
                string ns = term.Substring(0, indexOf);
                return new Uri(context[ns] + term.Substring(indexOf + 1));
            }

            return new Uri(context["@vocab"] + term);
        }

        public static string GetNuspecRelativeAddress(XDocument document)
        {
            XNamespace nuget = XNamespace.Get("http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd");

            XElement package = document.Element(nuget.GetName("package"));

            if (package == null)
            {
                throw new ArgumentException("document, missing <package>");
            }

            XElement metadata = package.Element(nuget.GetName("metadata"));

            if (metadata == null)
            {
                throw new ArgumentException("document, missing <metadata>");
            }

            string id = metadata.Element(nuget.GetName("id")).Value;
            string version = metadata.Element(nuget.GetName("version")).Value;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(version))
            {
                throw new ArgumentException("document, missing <id> or <version>");
            }

            string relativeAddress = id.ToLowerInvariant() + "." + version.ToLowerInvariant() + ".xml";

            return relativeAddress;
        }

        public static IEnumerable<IEnumerable<T>> Partition<T>(IEnumerable<T> source, int size)
        {
            T[] array = null;
            int count = 0;
            foreach (T item in source)
            {
                if (array == null)
                {
                    array = new T[size];
                }
                array[count] = item;
                count++;
                if (count == size)
                {
                    yield return new ReadOnlyCollection<T>(array);
                    array = null;
                    count = 0;
                }
            }
            if (array != null)
            {
                Array.Resize(ref array, count);
                yield return new ReadOnlyCollection<T>(array);
            }
        }

        //  where the property exists on the graph being merged in remove it from the existing graph
        public static void RemoveExistingProperties(IGraph existingGraph, IGraph graphToMerge, Uri[] properties)
        {
            foreach (Uri property in properties)
            {
                foreach (Triple t1 in graphToMerge.GetTriplesWithPredicate(graphToMerge.CreateUriNode(property)))
                {
                    INode subject = t1.Subject.CopyNode(existingGraph);
                    INode predicate = t1.Predicate.CopyNode(existingGraph);

                    IList<Triple> retractList = new List<Triple>(existingGraph.GetTriplesWithSubjectPredicate(subject, predicate));
                    foreach (Triple t2 in retractList)
                    {
                        existingGraph.Retract(t2);
                    }
                }
            }
        }
    }
}

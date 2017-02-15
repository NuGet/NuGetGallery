// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using JsonLD.Core;
using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.JsonLDIntegration;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Writing;

namespace NuGet.Services.Metadata.Catalog
{
    public static class Utils
    {
        private const string XslTransformNuSpec = "xslt.nuspec.xslt";
        private const string XslTransformNormalizeNuSpecNamespace = "xslt.normalizeNuspecNamespace.xslt";

        private static readonly Lazy<XslCompiledTransform> XslTransformNuSpecCache = new Lazy<XslCompiledTransform>(() => SafeLoadXslTransform(XslTransformNuSpec));
        private static readonly Lazy<XslCompiledTransform> XslTransformNormalizeNuSpecNamespaceCache = new Lazy<XslCompiledTransform>(() => SafeLoadXslTransform(XslTransformNormalizeNuSpecNamespace));

        private static readonly Dictionary<string, string> _resourceStringCache = new Dictionary<string, string>();

        public static Stream GetResourceStream(string resName)
        {
            string name = Assembly.GetExecutingAssembly().GetName().Name;
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(name + "." + resName);
        }

        public static string GetResource(string resName)
        {
            if (!_resourceStringCache.ContainsKey(resName))
            {
                using (var reader = new StreamReader(GetResourceStream(resName)))
                {
                    _resourceStringCache[resName] = reader.ReadToEnd();
                }
            }

            return _resourceStringCache[resName];
        }
        
        public static IGraph CreateNuspecGraph(XDocument nuspec, string baseAddress, bool normalizeXml = false)
        {
            XsltArgumentList arguments = new XsltArgumentList();
            arguments.AddParam("base", "", baseAddress);
            arguments.AddParam("extension", "", ".json");

            arguments.AddExtensionObject("urn:helper", new XsltHelper());

            nuspec = SafeXmlTransform(nuspec.CreateReader(), XslTransformNormalizeNuSpecNamespaceCache.Value);
            var rdfxml = SafeXmlTransform(nuspec.CreateReader(), XslTransformNuSpecCache.Value, arguments);

            var doc = SafeCreateXmlDocument(rdfxml.CreateReader());
            if (normalizeXml)
            {
                NormalizeXml(doc);
            }

            RdfXmlParser rdfXmlParser = new RdfXmlParser();
            IGraph graph = new Graph();
            rdfXmlParser.Load(graph, doc);

            return graph;
        }

        private static void NormalizeXml(XmlNode xmlNode)
        {
            if (xmlNode.Attributes != null)
            {
                foreach (XmlAttribute attribute in xmlNode.Attributes)
                {
                    attribute.Value = attribute.Value.Normalize(NormalizationForm.FormC);
                }
            }

            if (xmlNode.Value != null)
            {
                xmlNode.Value = xmlNode.Value.Normalize(NormalizationForm.FormC);
                return;
            }

            foreach (XmlNode childNode in xmlNode.ChildNodes)
            {
                NormalizeXml(childNode);
            }
        }

        internal static XmlDocument SafeCreateXmlDocument(XmlReader reader = null)
        {
            // CodeAnalysis / XmlDocument: set the resolver to null or instance
            var xmlDoc = new XmlDocument();
            xmlDoc.XmlResolver = null;

            if (reader != null)
            {
                xmlDoc.Load(reader);
            }

            return xmlDoc;
        }

        private static XDocument SafeXmlTransform(XmlReader reader, XslCompiledTransform transform, XsltArgumentList arguments = null)
        {
            XDocument result = new XDocument();
            using (XmlWriter writer = result.CreateWriter())
            {
                if (arguments == null)
                {
                    arguments = new XsltArgumentList();
                }

                // CodeAnalysis / XslCompiledTransform.Transform: set resolver property to null or instance
                transform.Transform(reader, arguments, writer, documentResolver: null);
            }
            return result;
        }

        private static XslCompiledTransform SafeLoadXslTransform(string resourceName)
        {
            var transform = new XslCompiledTransform();

            // CodeAnalysis / XmlReader.Create: provide settings instance and set resolver property to null or instance
            var settings = new XmlReaderSettings();
            settings.XmlResolver = null;

            var reader = XmlReader.Create(new StreamReader(GetResourceStream(resourceName)), settings);

            // CodeAnalysis / XslCompiledTransform.Load: specify default settings or set resolver property to null or instance
            transform.Load(reader, XsltSettings.Default, stylesheetResolver: null);
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
            parser.Load(g, new StreamReader(GetResourceStream(name)));
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

        public static JToken CreateJson(IGraph graph, JToken frame = null)
        {
            System.IO.StringWriter writer = new System.IO.StringWriter();
            IRdfWriter rdfWriter = new JsonLdWriter();
            rdfWriter.Save(graph, writer);
            writer.Flush();

            if (frame == null)
            {
                return JToken.Parse(writer.ToString());
            }
            else
            {
                JToken flattened = JToken.Parse(writer.ToString());
                JObject framed = JsonLdProcessor.Frame(flattened, frame, new JsonLdOptions());
                JObject compacted = JsonLdProcessor.Compact(framed, frame["@context"], new JsonLdOptions());

                return JsonSort.OrderJson(compacted);
            }
        }
        
        public static string CreateArrangedJson(IGraph graph, JToken frame = null)
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

                var arranged = JsonSort.OrderJson(compacted);

                return arranged.ToString();
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
            rdfReader.Load(graph, new StringReader(flattened.ToString(Newtonsoft.Json.Formatting.None, new Newtonsoft.Json.JsonConverter[0])));

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

        public static bool IsType(JToken context, JToken obj, Uri type)
        {
            JToken objTypeToken;
            if (((JObject)obj).TryGetValue("@type", out objTypeToken))
            {
                if (objTypeToken is JArray)
                {
                    foreach (JToken typeToken in ((JArray)objTypeToken).Values())
                    {
                        Uri objType = Expand(context, typeToken);

                        if (objType.AbsoluteUri == type.AbsoluteUri)
                        {
                            return true;
                        }
                    }
                    return false;
                }
                else
                {
                    Uri objType = Expand(context, objTypeToken);

                    return objType.AbsoluteUri == type.AbsoluteUri;
                }
            }
            return false;
        }

        public static bool IsType(JToken context, JObject obj, Uri[] types)
        {
            foreach (Uri type in types)
            {
                if (IsType(context, obj, type))
                {
                    return true;
                }
            }
            return false;
        }

        public static Uri Expand(JToken context, JToken token)
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
                return new Uri(context[ns].ToString() + term.Substring(indexOf + 1));
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

        public static string GenerateHash(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            using (HashAlgorithm hashAlgorithm = HashAlgorithm.Create("SHA512"))
            {
                return Convert.ToBase64String(hashAlgorithm.ComputeHash(stream));
            }
        }

        public static IEnumerable<PackageEntry> GetEntries(ZipArchive package)
        {
            IList<PackageEntry> result = new List<PackageEntry>();

            foreach (ZipArchiveEntry entry in package.Entries)
            {
                if (entry.FullName.EndsWith("/.rels", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.FullName.EndsWith("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (entry.FullName.EndsWith(".psmdcp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new PackageEntry(entry));
            }

            return result;
        }

        public static NupkgMetadata GetNupkgMetadata(Stream stream)
        {
            var nupkgMetadata = new NupkgMetadata
            {
                PackageSize = stream.Length,
                PackageHash = GenerateHash(stream)
            };

            stream.Seek(0, SeekOrigin.Begin);

            using (ZipArchive package = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                nupkgMetadata.Nuspec = GetNuspec(package);

                if (nupkgMetadata.Nuspec == null)
                {
                    throw new InvalidDataException("Unable to find nuspec");
                }

                nupkgMetadata.Entries = GetEntries(package);

                return nupkgMetadata;
            }
        }

        public static CatalogItem CreateCatalogItem(string origin, Stream stream, DateTime createdDate, DateTime? lastEditedDate = null, DateTime? publishedDate = null, string licenseNames = null, string licenseReportUrl = null)
        {
            try
            {
                NupkgMetadata nupkgMetadata = GetNupkgMetadata(stream);
                return new PackageCatalogItem(nupkgMetadata, createdDate, lastEditedDate, publishedDate);
            }
            catch (InvalidDataException e)
            {
                Trace.TraceError("Exception: {0} {1} {2}", origin, e.GetType().Name, e.Message);
                return null;
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Exception processsing {0}", origin), e);
            }
        }
                
        public static void TraceException(Exception e)
        {
            if (e is AggregateException)
            {
                foreach (Exception ex in ((AggregateException)e).InnerExceptions)
                {
                    TraceException(ex);
                }
            }
            else
            {
                Trace.TraceError("{0} {1}", e.GetType().Name, e.Message);
                Trace.TraceError("{0}", e.StackTrace);

                if (e.InnerException != null)
                {
                    TraceException(e.InnerException);
                }
            }
        }
    }
}

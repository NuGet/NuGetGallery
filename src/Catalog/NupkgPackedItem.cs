using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public class NupkgPackedItem : AppendOnlyCatalogItem
    {
        private readonly FileInfo _file;
        private Stream _stream;
        private IUriNode _mainNode;
        private Uri _mainUri;
        private Uri _baseAddress;

        public NupkgPackedItem(Uri baseAddress, string fullPath)
        {
            _file = new FileInfo(fullPath);

            _baseAddress = baseAddress;
        }

        public Uri IRI
        {
            get
            {
                return _mainUri;
            }
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            var task = GetPackedInfo();

            JObject frame = context.GetJsonLdContext("context.PackageDetails.json", GetItemType());

            task.Wait();
            StorageContent content = new StringStorageContent(Utils.CreateJson(task.Result, frame), "application/json", "no-store");

            return content;
        }


        private async Task<IGraph> GetPackedInfo()
        {
            using (_stream = _file.OpenRead())
            {

                IGraph graph = null;

                // work requiring the stream is done in the background, but only one task gets the stream at a time
                // to avoid creating multiple copies of what could be a very large stream.
                var graphTask = Task<IGraph>.Run(() => GetPackedGraph());
                var zipTask = Task<IEnumerable<PackageEntry>>.Run(() => GetEntries());
                var hashTask = Task<string>.Run(() => GenerateHash());


                // Start with the packed manifest graph
                graph = await graphTask;

                IUriNode typeNode = graph.CreateUriNode("rdf:type");
                IUriNode manifestType = graph.CreateUriNode("packed:Manifest");

                IUriNode localMain = graph.GetTriplesWithPredicateObject(typeNode, manifestType).Single().Subject as IUriNode;

                string id = ((ILiteralNode)graph.GetTriplesWithSubjectPredicate(localMain, graph.CreateUriNode("nuget:id")).Single().Object).Value;
                NuGetVersion version = NuGetVersion.Parse(((ILiteralNode)graph.GetTriplesWithSubjectPredicate(localMain, graph.CreateUriNode("nuget:version")).Single().Object).Value);

                // create the new IRI
                string mainIRI = String.Format(CultureInfo.InvariantCulture, "{0}{1}{2}.{3}.json", _baseAddress.AbsoluteUri,
                                                    _baseAddress.AbsoluteUri.EndsWith("/") ? null : "/", id, version.ToNormalizedString())
                                                    .ToLowerInvariant();

                _mainUri = new Uri(mainIRI);
                _mainNode = graph.CreateUriNode(_mainUri);

                // Update the local IRI to match the IRI we are going to use.
                ReplaceIRI(graph, localMain.Uri, _mainUri);

                // Add the PackageDetails type
                graph.Assert(_mainNode, typeNode, graph.CreateUriNode(Schema.DataTypes.PackageDetails));

                // catalog namespace
                INode timeStampPredicate = graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp);
                INode commitIdPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCommitId);

                graph.Assert(_mainNode, timeStampPredicate, graph.CreateLiteralNode(TimeStamp.ToString("O"), Schema.DataTypes.DateTime));
                graph.Assert(_mainNode, commitIdPredicate, graph.CreateLiteralNode(CommitId.ToString()));

                //  published
                INode publishedPredicate = graph.CreateUriNode(Schema.Predicates.Published);
                DateTime published = GetPublished() ?? TimeStamp;
                graph.Assert(_mainNode, publishedPredicate, graph.CreateLiteralNode(published.ToString("O"), Schema.DataTypes.DateTime));

                // Add zip file info
                graph.Assert(_mainNode, graph.CreateUriNode(Schema.Predicates.PackageHashAlgorithm), graph.CreateLiteralNode("SHA512"));
                graph.Assert(_mainNode, graph.CreateUriNode(Schema.Predicates.PackageHash), graph.CreateLiteralNode(await hashTask));
                graph.Assert(_mainNode, graph.CreateUriNode(Schema.Predicates.PackageSize), graph.CreateLiteralNode(GetPackageSize().ToString(), Schema.DataTypes.Integer));

                // zip entries
                var entries = await zipTask;
                if (entries != null && entries.Any())
                {
                    INode packageEntryPredicate = graph.CreateUriNode(Schema.Predicates.PackageEntry);
                    INode packageEntryType = graph.CreateUriNode(Schema.DataTypes.PackageEntry);
                    INode fullNamePredicate = graph.CreateUriNode(Schema.Predicates.FullName);
                    INode namePredicate = graph.CreateUriNode(Schema.Predicates.Name);
                    INode lengthPredicate = graph.CreateUriNode(Schema.Predicates.Length);
                    INode compressedLengthPredicate = graph.CreateUriNode(Schema.Predicates.CompressedLength);

                    foreach (PackageEntry entry in entries)
                    {
                        Uri entryUri = new Uri(mainIRI + "#" + entry.FullName);

                        INode entryNode = graph.CreateUriNode(entryUri);

                        graph.Assert(_mainNode, packageEntryPredicate, entryNode);
                        graph.Assert(entryNode, typeNode, packageEntryType);
                        graph.Assert(entryNode, fullNamePredicate, graph.CreateLiteralNode(entry.FullName));
                        graph.Assert(entryNode, namePredicate, graph.CreateLiteralNode(entry.Name));
                        graph.Assert(entryNode, lengthPredicate, graph.CreateLiteralNode(entry.Length.ToString(), Schema.DataTypes.Integer));
                        graph.Assert(entryNode, compressedLengthPredicate, graph.CreateLiteralNode(entry.CompressedLength.ToString(), Schema.DataTypes.Integer));
                    }
                }

                //VDS.RDF.Writing.CompressingTurtleWriter writer = new VDS.RDF.Writing.CompressingTurtleWriter();
                //writer.Save(graph, "c:\\data\\out.ttl");

                return graph;
            }
        }

        private static void ReplaceIRI(IGraph graph, Uri oldIRI, Uri newIRI)
        {
            // replace the local IRI with the NuGet IRI
            string localUri = oldIRI.AbsoluteUri;

            var triples = graph.Triples.ToArray();

            string mainIRI = newIRI.AbsoluteUri;

            foreach (var triple in triples)
            {
                IUriNode subject = triple.Subject as IUriNode;
                IUriNode objNode = triple.Object as IUriNode;
                INode newSubject = triple.Subject;
                INode newObject = triple.Object;

                bool replace = false;

                if (subject != null && subject.Uri.AbsoluteUri.StartsWith(localUri))
                {
                    // TODO: store these mappings in a dictionary
                    Uri iri = new Uri(String.Format(CultureInfo.InvariantCulture, "{0}{1}", mainIRI, subject.Uri.AbsoluteUri.Substring(localUri.Length)));
                    newSubject = graph.CreateUriNode(iri);
                    replace = true;
                }

                if (objNode != null && objNode.Uri.AbsoluteUri.StartsWith(localUri))
                {
                    // TODO: store these mappings in a dictionary
                    Uri iri = new Uri(String.Format(CultureInfo.InvariantCulture, "{0}{1}", mainIRI, objNode.Uri.AbsoluteUri.Substring(localUri.Length)));
                    newObject = graph.CreateUriNode(iri);
                    replace = true;
                }

                if (replace)
                {
                    graph.Assert(newSubject, triple.Predicate, newObject);
                    graph.Retract(triple);
                }
            }
        }

        // TODO: Implement this with the gallery date
        protected virtual DateTime? GetPublished()
        {
            return null;
        }

        private IGraph GetPackedGraph()
        {
            lock (_stream)
            {
                _stream.Seek(0, SeekOrigin.Begin);
                ZipFileSystem zip = new ZipFileSystem(_stream);

                using (PackageReader packageReader = new PackageReader(zip))
                {
                    CatalogPageReader packedReader = new CatalogPageReader(packageReader);
                    return PackagingGraphToDotNetRDF(packedReader.GetGraph());
                }
            }
        }

        /// <summary>
        /// Convert a packaging graph
        /// </summary>
        private static IGraph PackagingGraphToDotNetRDF(NuGet.Packaging.Catalog.Graph graph)
        {
            VDS.RDF.Graph result = new VDS.RDF.Graph();
            result.NamespaceMap.AddNamespace("nuget", new Uri("http://schema.nuget.org/schema#"));
            result.NamespaceMap.AddNamespace("packed", new Uri("http://schema.nuget.org/packed#"));
            result.NamespaceMap.AddNamespace("catalog", new Uri("http://schema.nuget.org/catalog#"));
            result.NamespaceMap.AddNamespace("xsd", new Uri("http://www.w3.org/2001/XMLSchema#"));
            result.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));

            foreach (var triple in graph.Triples)
            {
                result.Assert(GetDotNetRDFNode(result, triple.Subject), GetDotNetRDFNode(result, triple.Predicate), GetDotNetRDFNode(result, triple.Object));
            }

            return result;
        }

        /// <summary>
        /// Convert a packaging node to a dotnetrdf node
        /// </summary>
        private static INode GetDotNetRDFNode(IGraph graph, JsonLD.Core.RDFDataset.Node node)
        {
            INode result = null;

            if (node.IsBlankNode())
            {
                result = graph.CreateBlankNode();
            }
            else if (node.IsIRI())
            {
                result = graph.CreateUriNode(new Uri(node.GetValue()));
            }
            else if (node.IsLiteral())
            {
                if (!String.IsNullOrEmpty(node.GetDatatype()) && node.GetDatatype() != "http://www.w3.org/2001/XMLSchema#string")
                {
                    result = graph.CreateLiteralNode(node.GetValue(), new Uri(node.GetDatatype()));
                }
                else
                {
                    result = graph.CreateLiteralNode(node.GetValue());
                }
            }

            return result;
        }

        private long GetPackageSize()
        {
            lock(_stream)
            {
                return _stream.Length;
            }
        }

        protected virtual string GenerateHash()
        {
            lock (_stream)
            {
                _stream.Seek(0, SeekOrigin.Begin);

                using (HashAlgorithm hashAlgorithm = HashAlgorithm.Create("SHA512"))
                {
                    return Convert.ToBase64String(hashAlgorithm.ComputeHash(_stream));
                }
            }
        }

        private IEnumerable<PackageEntry> GetEntries()
        {
            lock (_stream)
            {
                _stream.Seek(0, SeekOrigin.Begin);
                var package = new ZipArchive(_stream, ZipArchiveMode.Read, true);

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
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.PackageDetails;
        }
    }
}

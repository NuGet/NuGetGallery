using Newtonsoft.Json.Linq;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class PackageCatalogItem : AppendOnlyCatalogItem
    {
        protected string _id;
        protected string _version;

        protected abstract XDocument GetNuspec();

        protected virtual DateTime? GetPublished()
        {
            return null;
        }

        protected virtual DateTime? GetCreated()
        {
            return null;
        }

        protected virtual DateTime? GetLastEdited()
        {
            return null;
        }

        protected virtual DateTime? GetRefreshed()
        {
            return null;
        }

        protected virtual IEnumerable<PackageEntry> GetEntries()
        {
            return null;
        }

        protected virtual long? GetPackageSize()
        {
            return null;
        }

        protected virtual string GetPackageHash()
        {
            return null;
        }

        // Additional catalog item sections
        protected virtual IEnumerable<GraphAddon> GetAddons()
        {
            return Enumerable.Empty<GraphAddon>();
        }

        public override IGraph CreateContentGraph(CatalogContext context)
        {
            XDocument original = GetNuspec();
            XDocument nuspec = NormalizeNuspecNamespace(original, context.GetXslt("xslt.normalizeNuspecNamespace.xslt"));
            IGraph graph = CreateNuspecGraph(nuspec, GetBaseAddress(), context.GetXslt("xslt.nuspec.xslt"));

            //  catalog infrastructure fields

            INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);
            INode permanentType = graph.CreateUriNode(Schema.DataTypes.Permalink);
            Triple resource = graph.GetTriplesWithPredicateObject(rdfTypePredicate, graph.CreateUriNode(GetItemType())).First();
            graph.Assert(resource.Subject, rdfTypePredicate, permanentType);

            //  published
            INode publishedPredicate = graph.CreateUriNode(Schema.Predicates.Published);
            DateTime published = GetPublished() ?? TimeStamp;
            graph.Assert(resource.Subject, publishedPredicate, graph.CreateLiteralNode(published.ToString("O"), Schema.DataTypes.DateTime));

            //  listed
            INode listedPredicated = graph.CreateUriNode(Schema.Predicates.Listed);
            Boolean listed = GetListed(published);
            graph.Assert(resource.Subject, listedPredicated, graph.CreateLiteralNode(listed.ToString(), Schema.DataTypes.Boolean));

            //  created
            INode createdPredicate = graph.CreateUriNode(Schema.Predicates.Created);
            DateTime created = GetCreated() ?? TimeStamp;
            graph.Assert(resource.Subject, createdPredicate, graph.CreateLiteralNode(created.ToString("O"), Schema.DataTypes.DateTime));

            //  lastEdited
            INode lastEditedPredicate = graph.CreateUriNode(Schema.Predicates.LastEdited);
            DateTime lastEdited = GetLastEdited() ?? DateTime.MinValue;
            graph.Assert(resource.Subject, lastEditedPredicate, graph.CreateLiteralNode(lastEdited.ToString("O"), Schema.DataTypes.DateTime));
            
            ////  refreshed
            //INode refreshedPredicate = graph.CreateUriNode(Schema.Predicates.Refreshed);
            //DateTime listed = GetRefreshed() ?? TimeStamp;
            //graph.Assert(resource.Subject, refreshedPredicate, graph.CreateLiteralNode(listed.ToString("O"), Schema.DataTypes.DateTime));

            //  entries

            IEnumerable<PackageEntry> entries = GetEntries();
            if (entries != null)
            {
                INode packageEntryPredicate = graph.CreateUriNode(Schema.Predicates.PackageEntry);
                INode packageEntryType = graph.CreateUriNode(Schema.DataTypes.PackageEntry);
                INode fullNamePredicate = graph.CreateUriNode(Schema.Predicates.FullName);
                INode namePredicate = graph.CreateUriNode(Schema.Predicates.Name);
                INode lengthPredicate = graph.CreateUriNode(Schema.Predicates.Length);
                INode compressedLengthPredicate = graph.CreateUriNode(Schema.Predicates.CompressedLength);

                foreach (PackageEntry entry in entries)
                {
                    Uri entryUri = new Uri(resource.Subject.ToString() + "#" + entry.FullName);

                    INode entryNode = graph.CreateUriNode(entryUri);

                    graph.Assert(resource.Subject, packageEntryPredicate, entryNode);
                    graph.Assert(entryNode, rdfTypePredicate, packageEntryType);
                    graph.Assert(entryNode, fullNamePredicate, graph.CreateLiteralNode(entry.FullName));
                    graph.Assert(entryNode, namePredicate, graph.CreateLiteralNode(entry.Name));
                    graph.Assert(entryNode, lengthPredicate, graph.CreateLiteralNode(entry.Length.ToString(), Schema.DataTypes.Integer));
                    graph.Assert(entryNode, compressedLengthPredicate, graph.CreateLiteralNode(entry.CompressedLength.ToString(), Schema.DataTypes.Integer));
                }
            }

            //  packageSize and packageHash

            long? packageSize = GetPackageSize();
            if (packageSize != null)
            {
                graph.Assert(resource.Subject, graph.CreateUriNode(Schema.Predicates.PackageSize), graph.CreateLiteralNode(packageSize.ToString(), Schema.DataTypes.Integer));
            }

            string packageHash = GetPackageHash();
            if (packageHash != null)
            {
                graph.Assert(resource.Subject, graph.CreateUriNode(Schema.Predicates.PackageHash), graph.CreateLiteralNode(packageHash));
                graph.Assert(resource.Subject, graph.CreateUriNode(Schema.Predicates.PackageHashAlgorithm), graph.CreateLiteralNode("SHA512"));
            }

            //  identity and version
            SetIdVersionFromGraph(graph);

            // apply addons
            foreach (GraphAddon addon in GetAddons())
            {
                addon.ApplyToGraph(graph, (IUriNode)resource.Subject);
            }

            return graph;
        }

        private bool GetListed(DateTime published)
        {
            //If the published date is 1900/01/01, then the package is unlisted
            if (published.ToUniversalTime() == Convert.ToDateTime("1900-01-01T00:00:00Z"))
            {
                return false;
            }
            return true;
        }

        protected void SetIdVersionFromGraph(IGraph graph)
        {
            INode idPredicate = graph.CreateUriNode(Schema.Predicates.Id);
            INode versionPredicate = graph.CreateUriNode(Schema.Predicates.Version);

            INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);
            Triple resource = graph.GetTriplesWithPredicateObject(rdfTypePredicate, graph.CreateUriNode(GetItemType())).First();
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
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            //  metadata from nuspec

            using (IGraph graph = CreateContentGraph(context))
            {
                //  catalog infrastructure fields

                INode rdfTypePredicate = graph.CreateUriNode(Schema.Predicates.Type);
                INode timeStampPredicate = graph.CreateUriNode(Schema.Predicates.CatalogTimeStamp);
                INode commitIdPredicate = graph.CreateUriNode(Schema.Predicates.CatalogCommitId);

                Triple resource = graph.GetTriplesWithPredicateObject(rdfTypePredicate, graph.CreateUriNode(GetItemType())).First();
                graph.Assert(resource.Subject, timeStampPredicate, graph.CreateLiteralNode(TimeStamp.ToString("O"), Schema.DataTypes.DateTime));
                graph.Assert(resource.Subject, commitIdPredicate, graph.CreateLiteralNode(CommitId.ToString()));

                //  create JSON content

                JObject frame = context.GetJsonLdContext("context.PackageDetails.json", GetItemType());

                StorageContent content = new StringStorageContent(Utils.CreateArrangedJson(graph, frame), "application/json", "no-store");

                return content;
            }
        }


        public override Uri GetItemType()
        {
            return Schema.DataTypes.PackageDetails;
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

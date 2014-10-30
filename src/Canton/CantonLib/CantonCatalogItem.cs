using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Services.Metadata.Catalog;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace NuGet.Canton
{
    /// <summary>
    /// Background loading pre-built page
    /// </summary>
    public class CantonCatalogItem : PackageCatalogItem, IComparable<CantonCatalogItem>
    {
        private ICloudBlob _blob;
        private CloudStorageAccount _account;
        private Uri _uri;
        private readonly int _cantonCommitId;
        private DateTime _published;

        // the catalog writer will dipose of this
        private Graph _graph;
        private bool _graphUriFixed;

        public CantonCatalogItem(CloudStorageAccount account, Uri uri, int cantonCommitId)
            : base()
        {
            _uri = uri;
            _account = account;
            _cantonCommitId = cantonCommitId;
            _graphUriFixed = false;
            _published = DateTime.MinValue;
        }

        /// <summary>
        /// Get the publish date from the graph. If the graph has not been created DateTime.MinValue is returned.
        /// </summary>
        public DateTime Published
        {
            get
            {
                if (_published == DateTime.MinValue)
                {
                    INode rdfTypePredicate = _graph.CreateUriNode(Schema.Predicates.Type);
                    Triple resource = _graph.GetTriplesWithPredicateObject(rdfTypePredicate, _graph.CreateUriNode(GetItemType())).First();

                    var pubTriple = _graph.GetTriplesWithSubjectPredicate(resource.Subject, _graph.CreateUriNode(Schema.Predicates.Published)).SingleOrDefault();

                    if  (pubTriple != null)
                    {
                        ILiteralNode node = pubTriple.Object as ILiteralNode;

                        if (node != null)
                        {
                            _published = DateTime.Parse(node.Value);
                        }
                    }
                }

                return _published;
            }
        }

        public int CantonCommitId
        {
            get
            {
                return _cantonCommitId;
            }
        }

        public void LoadGraph()
        {
            var client = _account.CreateCloudBlobClient();
            _blob = client.GetBlobReferenceFromServer(_uri);

            using (MemoryStream stream = new MemoryStream())
            {
                _blob.DownloadToStream(stream);
                stream.Seek(0, SeekOrigin.Begin);

                _graph = new Graph();

                using (StreamReader reader = new StreamReader(stream))
                {
                    TurtleParser parser = new TurtleParser();
                    parser.Load(_graph, reader);
                }
            }

            SetIdVersionFromGraph(_graph);
        }

        public async Task DeleteBlob()
        {
            await _blob.DeleteIfExistsAsync();
        }

        protected override XDocument GetNuspec()
        {
            throw new NotImplementedException();
        }

        public override IGraph CreateContentGraph(CatalogContext context)
        {
            if (!_graphUriFixed)
            {
                _graphUriFixed = true;

                INode rdfTypePredicate = _graph.CreateUriNode(Schema.Predicates.Type);
                Triple resource = _graph.GetTriplesWithPredicateObject(rdfTypePredicate, _graph.CreateUriNode(GetItemType())).First();

                Uri oldUri = ((IUriNode)resource.Subject).Uri;
                Uri newUri = GetItemAddress();

                CantonUtilities.ReplaceIRI(_graph, oldUri, newUri);
            }

            return _graph;
        }

        private StorageContent _content;
        public override StorageContent CreateContent(CatalogContext context)
        {
            if (_content == null)
            {
                _content = base.CreateContent(context);
            }

            return _content;
        }

        private IGraph _pageContent;
        public override IGraph CreatePageContent(CatalogContext context)
        {
            if (_pageContent == null)
            {
                _pageContent = base.CreatePageContent(context);
            }

            return _pageContent;
        }

        public int CompareTo(CantonCatalogItem other)
        {
            return CantonCommitId.CompareTo(other.CantonCommitId);
        }

        public static int Compare(CantonCatalogItem x, CantonCatalogItem y)
        {
            return x.CompareTo(y);
        }
    }
}

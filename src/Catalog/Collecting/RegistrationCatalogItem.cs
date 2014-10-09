using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.IO;
using System.Linq;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class RegistrationCatalogItem : CatalogItem
    {
        Uri _catalogUri;
        IGraph _catalogItem;
        Uri _itemAddress;
        string _registrationBaseAddress;
        string _contentBaseAddress;

        public RegistrationCatalogItem(string catalogUri, IGraph catalogItem, Uri registrationBaseAddress, string contentBaseAddress)
        {
            _catalogUri = new Uri(catalogUri);
            _catalogItem = catalogItem;
            _registrationBaseAddress = registrationBaseAddress.ToString().TrimEnd('/') + '/';
            _contentBaseAddress = contentBaseAddress.ToString().TrimEnd('/') + '/';
        }

        public override StorageContent CreateContent(CatalogContext context)
        {
            return null;
        }

        public override Uri GetItemType()
        {
            return Schema.DataTypes.Package;
        }

        public override Uri GetItemAddress()
        {
            return _itemAddress;
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            try
            {
                IGraph content;

                using (TripleStore store = new TripleStore())
                {
                    store.Add(_catalogItem, true);

                    SparqlParameterizedString sparql = new SparqlParameterizedString();
                    sparql.CommandText = Utils.GetResource("sparql.ConstructPackagePageContentGraph.rq");

                    sparql.SetUri("catalogPackage", _catalogUri);
                    sparql.SetLiteral("baseAddress", _registrationBaseAddress);
                    sparql.SetLiteral("contentBase", _contentBaseAddress);

                    content = SparqlHelpers.Construct(store, sparql.ToString());
                }

                _itemAddress = GetPackageInfoAddress(content);

                return content;
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Exception processing catalog item {0}", _catalogUri), e);
            }
        }

        Uri GetPackageInfoAddress(IGraph packageGraph)
        {
            Triple triple = packageGraph.GetTriplesWithPredicateObject(
                packageGraph.CreateUriNode(Schema.Predicates.Type),
                packageGraph.CreateUriNode(Schema.DataTypes.Package)).FirstOrDefault();

            return ((IUriNode)triple.Subject).Uri;
        }
    }
}

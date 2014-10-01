using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Maintenance;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using VDS.RDF;
using VDS.RDF.Query;

namespace NuGet.Services.Metadata.Catalog.Collecting
{
    public class RegistrationCatalogItem : CatalogItem
    {
        Uri _catalogUri;
        IGraph _catalogItem;
        string _registrationBaseAddress;
        string _contentBaseAddress;

        public RegistrationCatalogItem(string catalogUri, IGraph catalogItem, string registrationBaseAddress, string contentBaseAddress)
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
            return _catalogUri;
        }

        public override IGraph CreatePageContent(CatalogContext context)
        {
            IGraph content;

            using (TripleStore store = new TripleStore())
            {
                store.Add(_catalogItem, true);

                SparqlParameterizedString sparql = new SparqlParameterizedString();
                sparql.CommandText = Utils.GetResource("sparql.ConstructPackagePageContentGraph.rq");

                sparql.SetLiteral("baseAddress", _registrationBaseAddress);
                sparql.SetLiteral("contentBase", _contentBaseAddress);

                content = SparqlHelpers.Construct(store, sparql.ToString());
            }

            return content;
        }
    }
}

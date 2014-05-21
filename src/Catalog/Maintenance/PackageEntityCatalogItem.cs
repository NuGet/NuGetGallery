using System;

namespace NuGet.Services.Metadata.Catalog.Maintenance
{
    public class PackageEntityCatalogItem : CatalogItem
    {
        public override string CreateContent(CatalogContext context)
        {
            throw new NotImplementedException();
        }

        public override Uri GetItemType()
        {
            throw new NotImplementedException();
        }

        protected override string GetItemIdentity()
        {
            throw new NotImplementedException();
        }
    }
}

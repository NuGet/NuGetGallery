using System;

namespace NuGet.Services.Metadata.Catalog
{
    public class ReindexCatalogItem : CatalogItem
    {
        Uri _itemAddress;
        Uri _itemType;

        public ReindexCatalogItem(Uri itemAddress, Uri itemType)
        {
            _itemAddress = itemAddress;
            _itemType = itemType;
        }

        public override Uri GetItemType()
        {
            return _itemType;
        }

        public override Uri GetItemAddress()
        {
            return _itemAddress;
        }
    }
}

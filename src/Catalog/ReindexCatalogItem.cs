// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Services.Metadata.Catalog;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class DocumentFixUp
    {
        private DocumentFixUp(bool applicable, List<CatalogCommitItem> itemList)
        {
            Applicable = applicable;
            ItemList = itemList;
        }

        public static DocumentFixUp IsNotApplicable()
        {
            return new DocumentFixUp(applicable: false, itemList: null);
        }

        public static DocumentFixUp IsApplicable(List<CatalogCommitItem> itemList)
        {
            if (itemList == null)
            {
                throw new ArgumentNullException(nameof(itemList));
            }

            return new DocumentFixUp(applicable: true, itemList: itemList);
        }

        public bool Applicable { get; } 
        public List<CatalogCommitItem> ItemList { get; }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Metadata.Catalog.Persistence;
using VDS.RDF;

namespace NuGet.Services.Metadata.Catalog
{
    public abstract class CatalogItem
    {
        public DateTime TimeStamp { get; set; }

        public Guid CommitId { get; set; }

        public Uri BaseAddress { get; set; }

        public abstract Uri GetItemType();

        public abstract Uri GetItemAddress();

        public virtual StorageContent CreateContent(CatalogContext context)
        {
            return null;
        }

        public virtual IGraph CreatePageContent(CatalogContext context)
        {
            return null;
        }

        /// <summary>
        /// Create the core graph used in CreateContent(context)
        /// </summary>
        public virtual IGraph CreateContentGraph(CatalogContext context)
        {
            return null;
        }
    }
}

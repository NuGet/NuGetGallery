// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.Metadata.Catalog
{
    public class SavePagesResult
    {
        public SavePagesResult(IDictionary<string, CatalogItemSummary> pageEntries, Uri previousPageUri)
        {
            PageEntries = pageEntries ?? throw new ArgumentNullException(nameof(pageEntries));
            PreviousPageUri = previousPageUri;
        }

        public IDictionary<string, CatalogItemSummary> PageEntries { get; }
        public Uri PreviousPageUri { get; }
    }
}
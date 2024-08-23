// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Changes related to the search and hijack indexes for a specific ID.
    /// </summary>
    public class IndexChanges
    {
        public IndexChanges(
            IReadOnlyDictionary<SearchFilters, SearchIndexChangeType> search,
            IReadOnlyDictionary<NuGetVersion, HijackDocumentChanges> hijack)
        {
            Search = search ?? throw new ArgumentNullException(nameof(search));
            Hijack = hijack ?? throw new ArgumentNullException(nameof(hijack));
        }

        public IReadOnlyDictionary<SearchFilters, SearchIndexChangeType> Search { get; }
        public IReadOnlyDictionary<NuGetVersion, HijackDocumentChanges> Hijack { get; }
    }
}

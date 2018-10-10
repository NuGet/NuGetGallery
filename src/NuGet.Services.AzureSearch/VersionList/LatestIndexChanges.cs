// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// Changes related to the latest status in indexes. For the search index, this can be all of the metadata on the
    /// document. For the hijack index, this only relates to the latest booleans. This type represents changes related
    /// to a single <see cref="SearchFilters"/> value (i.e. one perspective of the indexes).
    /// </summary>
    public class LatestIndexChanges
    {
        public LatestIndexChanges(SearchIndexChangeType search, IReadOnlyList<HijackIndexChange> hijack)
        {
            Search = search;
            Hijack = hijack ?? throw new ArgumentNullException(nameof(hijack));
        }

        public SearchIndexChangeType Search { get; }
        public IReadOnlyList<HijackIndexChange> Hijack { get; }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Protocol.Catalog;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class LatestCatalogLeaves
    {
        public LatestCatalogLeaves(
            ISet<NuGetVersion> unavailable,
            IReadOnlyDictionary<NuGetVersion, PackageDetailsCatalogLeaf> available)
        {
            Unavailable = unavailable ?? throw new ArgumentNullException(nameof(unavailable));
            Available = available ?? throw new ArgumentNullException(nameof(available));
        }

        public ISet<NuGetVersion> Unavailable { get; }
        public IReadOnlyDictionary<NuGetVersion, PackageDetailsCatalogLeaf> Available { get; }
    }
}

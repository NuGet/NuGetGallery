// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Services.Metadata.Catalog;

namespace NgTests.Validation
{
    public sealed class CatalogLeaf
    {
        [JsonProperty("created")]
        public DateTimeOffset Created { get; set; }
        [JsonProperty("lastEdited")]
        public DateTimeOffset LastEdited { get; set; }

        [JsonProperty("packageEntries")]
        public IEnumerable<PackageEntry> PackageEntries { get; set; }
    }
}
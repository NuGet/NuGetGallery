// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.BasicSearchTests.Models
{
    public class GalleryQueryResult
    {
        public int? TotalHits { get; set; }

        public DateTime? IndexTimestamp { get; set; }

        public string Index { get; set; }

        public IEnumerable<GalleryPackage> Data { get; set; }
    }
}
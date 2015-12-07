// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Services.BasicSearchTests.Models
{
    public class V2SearchResult
    {
        public int? TotalHits { get; set; }

        public DateTime? IndexTimestamp { get; set; }

        public string Index { get; set; }

        public IList<V2Package> Data { get; set; }

        public V2Package GetPackage(string id)
        {
            return Data.FirstOrDefault(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }

        public bool ContainsPackage(string id)
        {
            return GetPackage(id) != null;
        }

        public string GetPackageVersion(string id)
        {
            return GetPackage(id)?.NormalizedVersion;
        }
    }
}
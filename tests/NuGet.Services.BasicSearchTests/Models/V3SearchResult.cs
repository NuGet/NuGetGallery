// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NuGet.Services.BasicSearchTests.Models
{
    public class V3SearchResult
    {
        [JsonProperty("@context")]
        public AtContext AtContext { get; set; }

        public int? TotalHits { get; set; }

        public DateTime? LastReopen { get; set; }

        public string Index { get; set; }

        public IList<V3Package> Data { get; set; }

        public V3Package GetPackage(string id)
        {
            return Data.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
        
        public bool ContainsPackage(string id)
        {
            return GetPackage(id) != null;
        }

        public bool ContainsPackageVersion(string id, string version)
        {
            var package = GetPackage(id);

            if (package != null)
            {
                return package.Versions.Any(v => String.Equals(v.Version, version, StringComparison.OrdinalIgnoreCase));
            }

            return false;
        }

        public string GetPackageVersion(string id)
        {
            return GetPackage(id)?.Version;
        }
    }
}
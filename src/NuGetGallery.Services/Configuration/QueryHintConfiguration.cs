// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NuGetGallery.Services
{
    public class QueryHintConfiguration : IQueryHintConfiguration
    {
        public QueryHintConfiguration() : this(Enumerable.Empty<string>())
        {
        }

        [JsonConstructor]
        public QueryHintConfiguration(IEnumerable<string> recompileForPackageDependents)
        {
            RecompileForPackageDependents = new HashSet<string>(
                recompileForPackageDependents ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
        }

        public HashSet<string> RecompileForPackageDependents { get; }

        public bool ShouldUseRecompileForPackageDependents(string packageId)
        {
            return RecompileForPackageDependents.Contains(packageId);
        }
    }
}
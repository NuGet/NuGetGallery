// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Indexing
{
    public class OwnersResult
    {
        public OwnersResult(
            HashSet<string> knownOwners,
            IDictionary<string, HashSet<string>> packagesWithOwners,
            IDictionary<string, IDictionary<string, DynamicDocIdSet>> mappings)
        {
            KnownOwners = knownOwners;
            PackagesWithOwners = packagesWithOwners;
            Mappings = mappings;
        }

        public HashSet<string> KnownOwners { get; }
        public IDictionary<string, HashSet<string>> PackagesWithOwners { get; }
        public IDictionary<string, IDictionary<string, DynamicDocIdSet>> Mappings { get; }
    }
}

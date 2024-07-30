// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Jobs.Catalog2Registration
{
    public class HiveMergeResult
    {
        public HiveMergeResult(HashSet<PageInfo> modifiedPages, HashSet<LeafInfo> modifiedLeaves, HashSet<LeafInfo> deletedLeaves)
        {
            ModifiedPages = modifiedPages;
            ModifiedLeaves = modifiedLeaves;
            DeletedLeaves = deletedLeaves;
        }

        public HashSet<PageInfo> ModifiedPages { get; }
        public HashSet<LeafInfo> ModifiedLeaves { get; }
        public HashSet<LeafInfo> DeletedLeaves { get; }
    }
}

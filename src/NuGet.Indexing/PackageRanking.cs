// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public abstract class PackageRanking
    {
        public abstract IDictionary<string, IDictionary<string, int>> GetProjectRankings();
        public abstract IDictionary<string, int> GetOverallRanking();
    }
}

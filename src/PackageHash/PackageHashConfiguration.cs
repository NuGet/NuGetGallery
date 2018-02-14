// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.PackageHash
{
    public class PackageHashConfiguration
    {
        public int BatchSize { get; set; }
        public int DegreeOfParallelism { get; set; }
        public IReadOnlyList<PackageSource> Sources { get; set; }
    }
}

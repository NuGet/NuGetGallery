// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Core;

namespace NuGet.Services.Metadata.Catalog.Monitoring
{
    public class PackageValidationResult
    {
        public PackageIdentity Package { get; set; }
        public IEnumerable<AggregateValidationResult> AggregateValidationResults { get; set; }
    }
}

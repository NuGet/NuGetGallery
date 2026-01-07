// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Services.Validation
{
    public class PackageCriteria : ICriteria
    {
        public IList<string> ExcludeOwners { get; set; } = new List<string>();

        public IList<string> IncludeIdPatterns { get; set; } = new List<string>();
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGet.Services.Revalidate
{
    public interface IPackageRevalidationInserter
    {
        Task AddPackageRevalidationsAsync(IReadOnlyList<PackageRevalidation> revalidations);
    }
}

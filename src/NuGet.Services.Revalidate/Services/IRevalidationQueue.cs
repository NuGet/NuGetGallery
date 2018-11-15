// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGet.Services.Revalidate
{
    public interface IRevalidationQueue
    {
        /// <summary>
        /// Fetch the next packages to revalidate.
        /// </summary>
        /// <returns>The next packages to revalidate, or an empty list if there are no packages to revalidate at this time.</returns>
        Task<IReadOnlyList<PackageRevalidation>> NextAsync();
    }
}

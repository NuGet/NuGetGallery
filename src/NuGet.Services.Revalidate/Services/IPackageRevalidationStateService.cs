// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Validation;

namespace NuGet.Services.Revalidate
{
    public interface IPackageRevalidationStateService
    {
        /// <summary>
        /// Add the new revalidations to the database.
        /// </summary>
        /// <returns>A task that completes once the revalidations have been saved.</returns>
        Task AddPackageRevalidationsAsync(IReadOnlyList<PackageRevalidation> revalidations);

        /// <summary>
        /// Remove package revalidation entities from the database.
        /// </summary>
        /// <returns>A task that returns the number of revalidations that have been removed.</returns>
        Task<int> RemovePackageRevalidationsAsync(int max);

        /// <summary>
        /// Count the number of package revalidations in the database.
        /// </summary>
        /// <returns>The count of package revalidations in the database.</returns>
        Task<int> PackageRevalidationCountAsync();

        /// <summary>
        /// Count the number of package revalidations that were enqueued in the past hour.
        /// </summary>
        /// <returns>The number of enqueued revalidations.</returns>
        Task<int> CountRevalidationsEnqueuedInPastHourAsync();

        /// <summary>
        /// Update the package revalidations and mark them as enqueued.
        /// </summary>
        /// <param name="revalidations">The revalidations to update.</param>
        /// <returns>A task that completes once the revalidations have been updated.</returns>
        Task MarkPackageRevalidationsAsEnqueuedAsync(IReadOnlyList<PackageRevalidation> revalidations);
    }
}

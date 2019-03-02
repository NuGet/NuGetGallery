// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface IPackageDeprecationService
    {
        /// <summary>
        /// Updates the deprecation of many packages.
        /// If any packages have an existing deprecation, it combines the existing state with the new state.
        /// If only a single package is provided, the existing deprecation is entirely replaced.
        /// If the deprecation supplied is not vulnerable, not legacy, and not other, the deprecations of the packages will be removed.
        /// Commits changes when finished.
        /// </summary>
        Task UpdateDeprecation(
            IReadOnlyCollection<Package> packages,
            PackageDeprecationStatus status,
            IReadOnlyCollection<Cve> cve,
            decimal? cvssRating,
            IReadOnlyCollection<Cwe> cwe,
            PackageRegistration alternatePackageRegistration,
            Package alternatePackage,
            string customMessage,
            bool shouldUnlist);

        /// <summary>
        /// Fetches all <see cref="Cve"/>s with a <see cref="Cve.CveId"/> contained in <paramref name="ids"/>.
        /// If an ID does not have an associated <see cref="Cve"/>, it creates a dummy entity.
        /// </summary>
        Task<IReadOnlyCollection<Cve>> GetOrCreateCvesByIdAsync(IEnumerable<string> ids, bool commitChanges);

        /// <summary>
        /// Fetches all <see cref="Cwe"/>s with a <see cref="Cwe.CweId"/> contained in <paramref name="ids"/>.
        /// </summary>
        IReadOnlyCollection<Cwe> GetCwesById(IEnumerable<string> ids);
    }
}
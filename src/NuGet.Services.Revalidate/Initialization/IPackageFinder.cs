// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace NuGet.Services.Revalidate
{
    public interface IPackageFinder
    {
        /// <summary>
        /// Find packages that are owned by the Microsoft account.
        /// </summary>
        /// <returns>The set of package registration keys owned by the Microsoft account.</returns>
        HashSet<int> FindMicrosoftPackages();

        /// <summary>
        /// Find packages that are preinstalled by Visual Studio and the .NET SDK.
        /// </summary>
        /// <param name="except">A set of package registration keys that should be removed from the result.</param>
        /// <returns>The set of package registration keys that are preinstalled.</returns>
        HashSet<int> FindPreinstalledPackages(HashSet<int> except);

        /// <summary>
        /// Find packages that the root set depends on, excluding the root self itself.
        /// </summary>
        /// <param name="roots">The set of root package registration keys whose dependencies should be fetched.</param>
        /// <returns>The set of packages registrations that are depended on by the root set.</returns>
        HashSet<int> FindDependencyPackages(HashSet<int> roots);

        /// <summary>
        /// Find all remaining packages.
        /// </summary>
        /// <param name="except">The set of registration keys that should be excluded from the result.</param>
        /// <returns>The set of package registrations keys, excluding the except set.</returns>
        HashSet<int> FindAllPackages(HashSet<int> except);

        /// <summary>
        /// Find information about the given set of packages.
        /// </summary>
        /// <param name="setName">The name of this set of packages.</param>
        /// <param name="packageRegistrationKeys">The set of package registration keys.</param>
        /// <returns>Information about each package registration, if it exists in the database.</returns>
        Task<List<PackageRegistrationInformation>> FindPackageRegistrationInformationAsync(string setName, HashSet<int> packageRegistrationKeys);

        /// <summary>
        /// Find versions that are appropriate for revalidations.
        /// </summary>
        /// <param name="packages">The packages whose versions should be fetched.</param>
        /// <returns>A map of package registration keys to the versions of that package registration.</returns>
        Dictionary<int, List<NuGetVersion>> FindAppropriateVersions(List<PackageRegistrationInformation> packages);

        /// <summary>
        /// Count how many packages are appropriate for revalidation.
        /// </summary>
        /// <returns>The count of packages appropriate for revalidation.</returns>
        int AppropriatePackageCount();
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public class GitHubUsageConfiguration : IGitHubUsageConfiguration
    {
        private readonly IReadOnlyDictionary<string, NuGetPackageGitHubInformation> _nuGetPackagesGitHubDependencies;

        public GitHubUsageConfiguration(IReadOnlyCollection<RepositoryInformation> repositories)
        {
            if (repositories == null)
            {
                throw new ArgumentNullException(nameof(repositories));
            }

            _nuGetPackagesGitHubDependencies = GetNuGetPackagesDependents(repositories);
        }

        public NuGetPackageGitHubInformation GetPackageInformation(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (_nuGetPackagesGitHubDependencies.TryGetValue(packageId, out var value))
            {
                return value;
            }

            return NuGetPackageGitHubInformation.Empty;
        }

        private static IReadOnlyDictionary<string, NuGetPackageGitHubInformation> GetNuGetPackagesDependents(IReadOnlyCollection<RepositoryInformation> repositories)
        {
            var dependentsPerPackage = new Dictionary<string, List<RepositoryInformation>>(StringComparer.OrdinalIgnoreCase);
            foreach (var repo in repositories)
            {
                foreach (var dependency in repo.Dependencies)
                {
                    if (!dependentsPerPackage.TryGetValue(dependency, out var packageDependents))
                    {
                        packageDependents = new List<RepositoryInformation>();
                        dependentsPerPackage[dependency] = packageDependents;
                    }

                    packageDependents.Add(repo);
                }
            }

            return dependentsPerPackage
                .ToDictionary(
                    entry => entry.Key,
                    entry => new NuGetPackageGitHubInformation(entry.Value),
                    StringComparer.OrdinalIgnoreCase);
        }
    }
}

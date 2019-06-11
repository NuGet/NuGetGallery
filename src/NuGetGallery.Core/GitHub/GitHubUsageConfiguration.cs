// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.GitHub
{
    public class GitHubUsageConfiguration : IGitHubUsageConfiguration
    {
        private readonly Dictionary<string, NuGetPackageGitHubInformation> _nuGetPackagesGitHubDependencies;

        public GitHubUsageConfiguration(IReadOnlyList<RepositoryInformation> repositories)
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

        private static Dictionary<string, NuGetPackageGitHubInformation> GetNuGetPackagesDependents(IReadOnlyList<RepositoryInformation> repositories)
        {
            var dependentsPerPackage = new Dictionary<string, List<RepositoryInformation>>(StringComparer.InvariantCultureIgnoreCase);
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
                    entry => new NuGetPackageGitHubInformation(
                             entry.Value.Count,
                             entry.Value),
                    StringComparer.InvariantCultureIgnoreCase);
        }
    }
}

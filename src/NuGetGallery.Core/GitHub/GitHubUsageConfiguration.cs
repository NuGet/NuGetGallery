using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.GitHub
{
    public class GitHubUsageConfiguration : IGitHubUsageConfiguration
    {
        public GitHubUsageConfiguration(IReadOnlyList<RepositoryInformation> repositories)
        {
            if (repositories == null)
            {
                throw new ArgumentNullException(nameof(repositories));
            }

            NuGetPackagesGitHubDependencies = repositories.Any()
                ? GetNuGetPackagesDependents(repositories)
                : new Dictionary<string, NuGetPackageGitHubInformation>();
        }

        private Dictionary<string, NuGetPackageGitHubInformation> NuGetPackagesGitHubDependencies { get; }

        public NuGetPackageGitHubInformation GetPackageInformation(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (NuGetPackagesGitHubDependencies.TryGetValue(packageId, out var value))
            {
                return value;
            }

            return NuGetPackageGitHubInformation.EMPTY;
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
                             entry.Value
                                  .OrderByDescending(x => x.Stars)
                                  .ThenBy(x => x.Id).Take(10).ToList()),
                    StringComparer.InvariantCultureIgnoreCase);
        }
    }
}

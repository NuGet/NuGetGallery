using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.GitHub
{
    public class GitHubUsageConfiguration : IGitHubUsageConfiguration
    {
        public const int ReposPerPackage = 10;
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
                             entry.Value
                                  .OrderByDescending(x => x.Stars)
                                  .ThenBy(x => x.Id).Take(ReposPerPackage).ToList()),
                    StringComparer.InvariantCultureIgnoreCase);
        }
    }
}

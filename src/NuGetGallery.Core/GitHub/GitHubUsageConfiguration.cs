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
                throw new ArgumentNullException(nameof(repositories) + " is null!");
            }

            NuGetPackagesGitHubDependencies = repositories.Any()
                ? GetNuGetPackagesDependents(repositories)
                : new Dictionary<string, NuGetPackageGitHubInformation>();
        }

        private Dictionary<string, NuGetPackageGitHubInformation> NuGetPackagesGitHubDependencies { get; }

        public NuGetPackageGitHubInformation GetPackageInformation(string packageId)
        {
            if (null == packageId)
            {
                throw new ArgumentException(string.Format("{0} cannot be null!", nameof(packageId)));
            }

            if (NuGetPackagesGitHubDependencies.TryGetValue(packageId, out var value))
            {
                return value;
            }

            return new NuGetPackageGitHubInformation();
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
                    entry =>
                    {
                        entry.Value.Sort(Comparer<RepositoryInformation>.Create((x, y) =>
                            {
                                var result = y.Stars.CompareTo(x.Stars); // Inverted for descending sort order
                                if (result != 0)
                                {
                                    return result;
                                }

                                // Results have the same star count, compare their ids (not inverted) to sort in alphabetical order
                                return string.Compare(x.Id, y.Id, true);
                            }));

                        return new NuGetPackageGitHubInformation(entry.Value.Count, entry.Value.Take(10).ToList().AsReadOnly());
                    },
                    StringComparer.InvariantCultureIgnoreCase);
        }
    }
}

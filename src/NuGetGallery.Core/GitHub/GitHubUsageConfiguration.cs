using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery.GitHub
{
    public class GitHubUsageConfiguration : IGitHubUsageConfiguration
    {
        public GitHubUsageConfiguration(IReadOnlyList<RepositoryInformation> reposCache)
        {
            NuGetPackagesGitHubDependencies = reposCache.Any()
                ? GitHubCacheTransformer.GetNuGetPackagesDependents(reposCache)
                : new Dictionary<string, NuGetPackageGitHubInformation>();
        }

        public Dictionary<string, NuGetPackageGitHubInformation> NuGetPackagesGitHubDependencies { get; }

        public NuGetPackageGitHubInformation GetPackageInformation(string packageId)
        {
            return NuGetPackagesGitHubDependencies.ContainsKey(packageId)
                ? NuGetPackagesGitHubDependencies[packageId]
                : new NuGetPackageGitHubInformation();
        }
    }
}

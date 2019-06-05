using System.Collections.Generic;

namespace NuGetGallery.GitHub
{
    public class NuGetPackageGitHubInformation
    {
        public NuGetPackageGitHubInformation() : this(0, null)
        { }

        public NuGetPackageGitHubInformation(int totalRepos, IReadOnlyList<RepositoryInformation> repos)
        {
            TotalRepos = totalRepos;
            Repos = repos;
        }

        public int TotalRepos { get; }
        public IReadOnlyList<RepositoryInformation> Repos { get; }
    }
}

using System.Collections.Generic;

namespace NuGetGallery.GitHub
{
    public class NuGetPackageGitHubInformation
    {
        public int TotalRepos { get; set; }
        public IReadOnlyList<RepositoryInformation> Repos { get; set; }

        public NuGetPackageGitHubInformation()
        {
            TotalRepos = 0;
            Repos = null;
        }
    }
}

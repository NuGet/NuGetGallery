using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
